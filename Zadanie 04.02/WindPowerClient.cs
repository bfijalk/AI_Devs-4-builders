using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zadanie_04._02;

public class WindPowerClient
{
    private const string VerifyUrl = "https://hub.ag3nts.org/verify";

    private readonly HttpClient _http;
    private readonly ILogger<WindPowerClient> _logger;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public WindPowerClient(ILogger<WindPowerClient> logger, string apiKey, HttpClient? httpClient = null)
    {
        _logger = logger;
        _apiKey = apiKey;
        _http = httpClient ?? new HttpClient();
    }

    public async Task<WindPowerApiResult> TrySendActionAsync(
        object answer,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            apikey = _apiKey,
            task = "windpower",
            answer,
        };

        var json = JsonSerializer.Serialize(payload);
        _logger.LogDebug("→ POST /verify  task=windpower");
        _logger.LogDebug("→ Body: {Body}", json);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(VerifyUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogDebug("← Status: {Status}", (int)response.StatusCode);
        _logger.LogDebug("← Response:\n{Body}", FormatJson(body));

        return WindPowerApiResult.FromResponse((int)response.StatusCode, body);
    }

    public async Task<JsonDocument> SendActionAsync(
        object answer,
        CancellationToken cancellationToken = default)
    {
        var result = await TrySendActionAsync(answer, cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Błąd API ({result.StatusCode}): {result.Body}");
        }

        return result.Document ?? JsonDocument.Parse(result.Body);
    }

    public Task<JsonDocument> GetHelpAsync(CancellationToken cancellationToken = default)
        => SendActionAsync(new { action = "help" }, cancellationToken);

    public static string FormatResponse(JsonDocument document)
        => JsonSerializer.Serialize(document.RootElement, JsonOpts);

    private static string FormatJson(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc, JsonOpts);
        }
        catch
        {
            return raw;
        }
    }
}
