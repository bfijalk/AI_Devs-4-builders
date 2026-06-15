using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Zadanie_03._03;

public class ReactorWebhook
{
    private const string BackendUrl = "https://hub.ag3nts.org/reactor_backend.php";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _outputDir;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public ReactorWebhook(string apiKey, string outputDir = "files")
    {
        _apiKey = apiKey;
        _outputDir = outputDir;
        _http = new HttpClient();
    }

    public async Task<string> RefreshBoardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await PageDownloader.DownloadReactorPreviewAsync(_outputDir, cancellationToken);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["key"] = _apiKey,
        });

        var response = await _http.PostAsync(BackendUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        Directory.CreateDirectory(_outputDir);
        var statePath = Path.Combine(_outputDir, "reactor_state.json");
        using var doc = JsonDocument.Parse(body);
        var formatted = JsonSerializer.Serialize(doc.RootElement, JsonOpts);
        await File.WriteAllTextAsync(statePath, formatted, cancellationToken);

        return statePath;
    }

    public WebApplication CreateServer(string url = "http://localhost:8080")
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(url);

        var app = builder.Build();

        app.MapPost("/webhook", async (CancellationToken cancellationToken) =>
        {
            var statePath = await RefreshBoardSnapshotAsync(cancellationToken);
            return Results.Json(new { ok = true, statePath });
        });

        return app;
    }
}
