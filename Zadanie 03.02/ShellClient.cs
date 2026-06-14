using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zadanie_03._02;

public class ShellClient
{
    private const string ShellUrl = "https://hub.ag3nts.org/api/shell";

    private readonly HttpClient _http;
    private readonly ILogger<ShellClient> _logger;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
    };

    public ShellClient(ILogger<ShellClient> logger, string apiKey)
    {
        _logger = logger;
        _apiKey = apiKey;
        _http = new HttpClient();
    }

    public async Task<JsonDocument> SendCommandAsync(string cmd, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            apikey = _apiKey,
            cmd,
        };

        while (true)
        {
            _logger.LogDebug("→ POST {Url}", ShellUrl);
            _logger.LogDebug("→ Body: {Body}", JsonSerializer.Serialize(payload, JsonOpts));

            var response = await _http.PostAsJsonAsync(ShellUrl, payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("← Status: {Status}", (int)response.StatusCode);
            _logger.LogDebug("← Response:\n{Body}", FormatJson(body));

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var waitSec = ExtractWaitSeconds(response, body, 30);
                _logger.LogWarning("← 403 — dostęp zablokowany. Czekam {Wait}s...", waitSec);
                await Task.Delay(TimeSpan.FromSeconds(waitSec), cancellationToken);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var waitSec = ExtractWaitSeconds(response, body, 10);
                _logger.LogWarning("← 429 — limit zapytań. Czekam {Wait}s...", waitSec);
                await Task.Delay(TimeSpan.FromSeconds(waitSec), cancellationToken);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                var waitSec = ExtractWaitSeconds(response, body, 5);
                _logger.LogWarning("← 503 — serwer niedostępny. Czekam {Wait}s...", waitSec);
                await Task.Delay(TimeSpan.FromSeconds(waitSec), cancellationToken);
                continue;
            }

            return JsonDocument.Parse(body);
        }
    }

    private static string FormatJson(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return raw;
        }
    }

    private static int ExtractWaitSeconds(HttpResponseMessage response, string body, int defaultVal)
    {
        var retryAfter = response.Headers
            .FirstOrDefault(h => h.Key.Equals("Retry-After", StringComparison.OrdinalIgnoreCase))
            .Value?.FirstOrDefault();
        if (retryAfter != null && int.TryParse(retryAfter, out var headerSec))
            return headerSec;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            foreach (var key in new[] { "retry_after", "retryAfter", "wait", "ban_duration" })
            {
                if (root.TryGetProperty(key, out var prop) && prop.TryGetInt32(out var sec))
                    return sec;
            }
        }
        catch
        {
            // ignoruj błędy parsowania
        }

        return defaultVal;
    }
}
