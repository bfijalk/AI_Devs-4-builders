using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zadanie_03._02;

public class VerifyClient
{
    private const string VerifyUrl = "https://hub.ag3nts.org/verify";

    private readonly HttpClient _http;
    private readonly ILogger<VerifyClient> _logger;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public VerifyClient(ILogger<VerifyClient> logger, string apiKey)
    {
        _logger = logger;
        _apiKey = apiKey;
        _http = new HttpClient();
    }

    public async Task<JsonDocument> SubmitConfirmationAsync(string confirmationCode, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            apikey = _apiKey,
            task = "firmware",
            answer = new { confirmation = confirmationCode },
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        _logger.LogInformation("→ POST {Url}\n{Body}", VerifyUrl, json);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(VerifyUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("← Status: {Status}\n{Body}", (int)response.StatusCode, FormatJson(body));
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(body);
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
