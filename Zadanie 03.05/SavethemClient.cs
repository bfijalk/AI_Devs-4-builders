using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zadanie_03._05;

public class SavethemClient
{
    private const string VerifyUrl = "https://hub.ag3nts.org/verify";

    private readonly HttpClient _http;
    private readonly ILogger<SavethemClient> _logger;
    private readonly string _apiKey;

    public SavethemClient(ILogger<SavethemClient> logger, string apiKey, HttpClient? httpClient = null)
    {
        _logger = logger;
        _apiKey = apiKey;
        _http = httpClient ?? new HttpClient();
    }

    public async Task<JsonDocument> SubmitRouteAsync(
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            apikey = _apiKey,
            task = "savethem",
            answer = commands,
        };

        var json = JsonSerializer.Serialize(payload);
        _logger.LogInformation("→ POST /verify  task=savethem  steps={Count}", commands.Count);
        _logger.LogDebug("→ Body: {Body}", json);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(VerifyUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("← Status: {Status}", (int)response.StatusCode);
        _logger.LogDebug("← Response: {Body}", body);

        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(body);
    }
}
