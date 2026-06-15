using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zadanie_03._03;

public class ReactorClient
{
    private const string VerifyUrl = "https://hub.ag3nts.org/verify";

    private readonly HttpClient _http;
    private readonly ILogger<ReactorClient> _logger;
    private readonly string _apiKey;
    private readonly ReactorWebhook? _webhook;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public ReactorClient(ILogger<ReactorClient> logger, string apiKey, ReactorWebhook? webhook = null)
    {
        _logger = logger;
        _apiKey = apiKey;
        _webhook = webhook;
        _http = new HttpClient();
    }

    public Task<JsonDocument> StartGameAsync(CancellationToken cancellationToken = default)
        => SendCommandAsync("start", cancellationToken);

    public async Task<JsonDocument> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            apikey = _apiKey,
            task = "reactor",
            answer = new { command },
        };

        var json = JsonSerializer.Serialize(payload);
        _logger.LogInformation("→ POST /verify  command={Command}", command);
        _logger.LogDebug("→ Body: {Body}", json);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(VerifyUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonDocument.Parse(body);

        _logger.LogInformation("← Status: {Status}", (int)response.StatusCode);
        _logger.LogDebug("← Response:\n{Body}", FormatJson(body));

        if (_webhook is not null)
        {
            try
            {
                var statePath = await _webhook.RefreshBoardSnapshotAsync(cancellationToken);
                _logger.LogDebug("← Webhook odświeżył stan: {Path}", statePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "← Webhook nie odświeżył stanu planszy");
            }
        }

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException(
                $"Błąd API ({(int)response.StatusCode}): {body}");
        }

        return result;
    }

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
