using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RailwayAgent;

public class RailwayClient
{
    private const string BaseUrl = "https://hub.ag3nts.org/verify";

    private readonly HttpClient _http;
    private readonly ILogger<RailwayClient> _logger;
    private readonly string _apiKey;

    // Wspólny czas resetu rate-limitu (epoch seconds)
    private double _rateLimitResetAt = 0;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public RailwayClient(ILogger<RailwayClient> logger, string apiKey)
    {
        _logger = logger;
        _apiKey = apiKey;
        _http = new HttpClient();
    }

    public async Task<JsonDocument> VerifyAsync(object answer)
    {
        var payload = new
        {
            apikey = _apiKey,
            task = "railway",
            answer,
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var answerJson = JsonSerializer.Serialize(answer, JsonOpts);

        while (true)
        {
            await WaitForRateLimitResetAsync();

            _logger.LogDebug("→ POST {Url}\n{Body}", BaseUrl, answerJson);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(BaseUrl, content);

            LogRateLimitHeaders(response);

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                var body = await TryReadJsonAsync(response);
                int waitSec = ExtractWaitSeconds(response, body, 5);
                _logger.LogWarning("← 503 — serwer przeciążony. Czekam {Wait}s przed ponowieniem...\n{Body}",
                    waitSec, body != null ? JsonSerializer.Serialize(body, JsonOpts) : "{}");
                await Task.Delay(TimeSpan.FromSeconds(waitSec));
                continue;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var body = await TryReadJsonAsync(response);
                int waitSec = ExtractWaitSeconds(response, body, 10);
                _logger.LogWarning("← 429 — przekroczono limit zapytań. Czekam {Wait}s...\n{Body}",
                    waitSec, body != null ? JsonSerializer.Serialize(body, JsonOpts) : "{}");
                await Task.Delay(TimeSpan.FromSeconds(waitSec));
                continue;
            }

            response.EnsureSuccessStatusCode();

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(resultJson);
            _logger.LogDebug("← {Status}\n{Body}", (int)response.StatusCode,
                JsonSerializer.Serialize(result, JsonOpts));
            return result;
        }
    }

    private void LogRateLimitHeaders(HttpResponseMessage response)
    {
        var relevant = response.Headers
            .Where(h => h.Key.Contains("ratelimit", StringComparison.OrdinalIgnoreCase)
                     || h.Key.Contains("rate-limit", StringComparison.OrdinalIgnoreCase)
                     || h.Key.Contains("retry", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

        if (relevant.Count > 0)
            _logger.LogDebug("Rate-limit headers:\n{Headers}", JsonSerializer.Serialize(relevant, JsonOpts));

        // Zaktualizuj czas resetu
        var resetRaw = response.Headers
            .FirstOrDefault(h => h.Key.Equals("X-RateLimit-Reset", StringComparison.OrdinalIgnoreCase)
                              || h.Key.Equals("RateLimit-Reset", StringComparison.OrdinalIgnoreCase))
            .Value?.FirstOrDefault();

        if (resetRaw != null && double.TryParse(resetRaw, out var val))
        {
            _rateLimitResetAt = val > 1_000_000_000
                ? val
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds() + val;
            _logger.LogDebug("Rate-limit reset za {Sec:F1}s", _rateLimitResetAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
    }

    private async Task WaitForRateLimitResetAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (_rateLimitResetAt > now)
        {
            var wait = _rateLimitResetAt - now;
            _logger.LogWarning("Rate-limit aktywny — czekam {Wait:F1}s do resetu...", wait);
            await Task.Delay(TimeSpan.FromSeconds(wait));
            _rateLimitResetAt = 0;
        }
    }

    private static async Task<JsonElement?> TryReadJsonAsync(HttpResponseMessage response)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(text);
        }
        catch
        {
            return null;
        }
    }

    private static int ExtractWaitSeconds(HttpResponseMessage response, JsonElement? body, int defaultVal)
    {
        // Sprawdź nagłówki HTTP
        var retryAfter = response.Headers
            .FirstOrDefault(h => h.Key.Equals("Retry-After", StringComparison.OrdinalIgnoreCase))
            .Value?.FirstOrDefault();
        if (retryAfter != null && int.TryParse(retryAfter, out var hSec))
            return hSec;

        // Sprawdź body JSON
        if (body.HasValue)
        {
            foreach (var key in new[] { "retry_after", "retryAfter", "wait" })
            {
                if (body.Value.TryGetProperty(key, out var prop) && prop.TryGetInt32(out var bSec))
                    return bSec;
            }
        }

        return defaultVal;
    }
}
