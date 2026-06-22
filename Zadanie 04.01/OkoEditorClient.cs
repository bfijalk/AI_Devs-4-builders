using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zadanie_04._01;

public class OkoEditorClient
{
    private const string VerifyUrl = "https://hub.ag3nts.org/verify";

    private readonly HttpClient _http;
    private readonly ILogger<OkoEditorClient> _logger;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public OkoEditorClient(ILogger<OkoEditorClient> logger, string apiKey)
    {
        _logger = logger;
        _apiKey = apiKey;
        _http = new HttpClient();
    }

    public async Task<OkoApiResult> TrySendActionAsync(
        object answer,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            apikey = _apiKey,
            task = "okoeditor",
            answer,
        };

        var json = JsonSerializer.Serialize(payload);
        _logger.LogInformation("→ POST /verify  task=okoeditor");
        _logger.LogDebug("→ Body: {Body}", json);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(VerifyUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("← Status: {Status}", (int)response.StatusCode);
        _logger.LogDebug("← Response:\n{Body}", FormatJson(body));

        return OkoApiResult.FromResponse((int)response.StatusCode, body);
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

    public Task<OkoApiResult> TryUpdateAsync(
        string page,
        string id,
        string? title = null,
        string? content = null,
        string? done = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) &&
            string.IsNullOrWhiteSpace(content) &&
            string.IsNullOrWhiteSpace(done))
        {
            throw new ArgumentException("Co najmniej jedno z pól title, content lub done musi być podane.");
        }

        var answer = new Dictionary<string, object?>
        {
            ["page"] = page,
            ["id"] = id,
            ["action"] = "update",
        };

        if (!string.IsNullOrWhiteSpace(title))
        {
            answer["title"] = title;
        }

        if (!string.IsNullOrWhiteSpace(content))
        {
            answer["content"] = content;
        }

        if (!string.IsNullOrWhiteSpace(done))
        {
            answer["done"] = done;
        }

        return TrySendActionAsync(answer, cancellationToken);
    }

    public async Task<JsonDocument> UpdateAsync(
        string page,
        string id,
        string? title = null,
        string? content = null,
        string? done = null,
        CancellationToken cancellationToken = default)
    {
        var result = await TryUpdateAsync(page, id, title, content, done, cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Błąd API ({result.StatusCode}): {result.Body}");
        }

        return result.Document ?? JsonDocument.Parse(result.Body);
    }

    public Task<OkoApiResult> TryDoneAsync(CancellationToken cancellationToken = default)
        => TrySendActionAsync(new { action = "done" }, cancellationToken);

    public async Task<JsonDocument> DoneAsync(CancellationToken cancellationToken = default)
    {
        var result = await TryDoneAsync(cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Błąd API ({result.StatusCode}): {result.Body}");
        }

        return result.Document ?? JsonDocument.Parse(result.Body);
    }

    public static string FormatResponse(JsonDocument document)
        => JsonSerializer.Serialize(document.RootElement, JsonOpts);

    public static string FormatResult(OkoApiResult result) => result.ToJson();

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
