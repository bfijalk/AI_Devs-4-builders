using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Zadanie_03._05;

public class ToolSearchClient
{
    public const string HubBaseUrl = "https://hub.ag3nts.org";
    private const string ToolSearchPath = "/api/toolsearch";
    private const string MapsPath = "/api/maps";
    private const string BooksPath = "/api/books";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly ILogger<ToolSearchClient> _logger;
    private readonly string _apiKey;

    public ToolSearchClient(ILogger<ToolSearchClient> logger, string apiKey, HttpClient? httpClient = null)
    {
        _logger = logger;
        _apiKey = apiKey;
        _http = httpClient ?? new HttpClient { BaseAddress = new Uri(HubBaseUrl) };
    }

    /// <summary>
    /// Searches for agent tools matching the given natural-language query (English only).
    /// </summary>
    public async Task<ToolSearchResponse> SearchToolsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<ToolSearchResponse>(ToolSearchPath, query, cancellationToken);

        _logger.LogInformation(
            "Found {Count} tool(s): {Names}",
            result.Tools.Count,
            string.Join(", ", result.Tools.Select(t => t.Name)));

        return result;
    }

    /// <summary>
    /// Fetches up to 3 best-matching notes from the books archive.
    /// </summary>
    public Task<BooksResponse> FetchBooksAsync(string query, CancellationToken cancellationToken = default)
        => PostAsync<BooksResponse>(BooksPath, query, cancellationToken);

    /// <summary>
    /// Fetches a 10x10 terrain map for the given city name.
    /// </summary>
    public async Task<TerrainMapResponse> FetchTerrainMapAsync(
        string cityName,
        CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<TerrainMapResponse>(MapsPath, cityName, cancellationToken);

        if (result.Code < 0)
            throw new InvalidOperationException($"Maps API error ({result.Code}): {result.Message}");

        _logger.LogInformation("Map loaded for {City} ({Rows}x{Cols})",
            result.CityName,
            result.Map.Length,
            result.Map.FirstOrDefault()?.Length ?? 0);

        return result;
    }

    /// <summary>
    /// Fetches vehicle stats and description from /api/wehicles.
    /// </summary>
    public async Task<VehicleInfo> FetchVehicleAsync(
        string vehicleName,
        CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<VehicleResponse>("/api/wehicles", vehicleName, cancellationToken);

        if (result.Code < 0)
            throw new InvalidOperationException($"Vehicles API error ({result.Code}): {result.Message}");

        return new VehicleInfo(
            result.Name,
            result.Note,
            new VehicleConsumption(result.Consumption.Fuel, result.Consumption.Food));
    }

    /// <summary>
    /// Uses toolsearch to locate the maps tool, reads books for usage hints, then fetches the terrain map.
    /// </summary>
    public async Task<TerrainMapResponse> FetchTerrainMapFromSearchAsync(
        string searchQuery,
        string cityName,
        CancellationToken cancellationToken = default)
    {
        var tools = await SearchToolsAsync(searchQuery, cancellationToken);

        var mapsTool = tools.Tools.FirstOrDefault(t => t.Name == "maps")
            ?? throw new InvalidOperationException("Toolsearch did not return the maps tool.");

        _logger.LogInformation("Maps tool found at {Url}: {Description}", mapsTool.Url, mapsTool.Description);

        var books = await FetchBooksAsync(searchQuery, cancellationToken);
        foreach (var note in books.Notes)
            _logger.LogInformation("Book note: {Title}", note.Title);

        return await FetchTerrainMapAsync(cityName, cancellationToken);
    }

    /// <summary>
    /// Calls a discovered tool endpoint. All hub tools accept the same payload as toolsearch.
    /// </summary>
    public async Task<string> QueryToolAsync(
        string toolUrl,
        string query,
        CancellationToken cancellationToken = default)
    {
        var path = toolUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(toolUrl).PathAndQuery
            : toolUrl;

        return await PostRawAsync(path, query, cancellationToken);
    }

    private async Task<T> PostAsync<T>(string path, string query, CancellationToken cancellationToken)
    {
        var body = await PostRawAsync(path, query, cancellationToken);
        return JsonSerializer.Deserialize<T>(body, JsonOpts)
            ?? throw new InvalidOperationException($"Empty response from {path}.");
    }

    private async Task<string> PostRawAsync(string path, string query, CancellationToken cancellationToken)
    {
        var payload = new ToolQueryRequest(_apiKey, query);

        _logger.LogInformation("→ POST {Path}  query={Query}", path, query);

        var response = await _http.PostAsJsonAsync(path, payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("← Status: {Status}", (int)response.StatusCode);
        _logger.LogDebug("← Response: {Body}", body);

        response.EnsureSuccessStatusCode();
        return body;
    }
}

public record ToolQueryRequest(
    [property: JsonPropertyName("apikey")] string ApiKey,
    [property: JsonPropertyName("query")] string Query);

public record ToolSearchResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("tools")] IReadOnlyList<AgentToolInfo> Tools);

public record AgentToolInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parameter")] string Parameter,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("matched_keywords")] IReadOnlyList<string> MatchedKeywords);

public record BooksResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("search_mode")] string? SearchMode,
    [property: JsonPropertyName("returned")] int Returned,
    [property: JsonPropertyName("notes")] IReadOnlyList<BookNote> Notes);

public record BookNote(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("matched_terms")] IReadOnlyList<string> MatchedTerms);

public record TerrainMapResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("cityName")] string CityName,
    [property: JsonPropertyName("map")] string[][] Map,
    [property: JsonPropertyName("text")] string Text);

public record VehicleResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("note")] string Note,
    [property: JsonPropertyName("consumption")] VehicleConsumptionResponse Consumption);

public record VehicleConsumptionResponse(
    [property: JsonPropertyName("fuel")] double Fuel,
    [property: JsonPropertyName("food")] double Food);
