using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Zadanie_03._05;

public sealed class SavethemAgentResult
{
    public required GameBriefing Briefing { get; init; }
    public required RoutePlan Route { get; init; }
    public JsonDocument? Response { get; init; }
    public string? Flag { get; init; }
    public bool Success => Flag is not null;
}

public class SavethemAgent
{
    private static readonly Regex FlagRegex = new(@"\{FLG:[A-Z0-9]+\}", RegexOptions.IgnoreCase);

    private readonly GameBriefingCollector _briefingCollector;
    private readonly SavethemClient _client;
    private readonly ILogger<SavethemAgent> _logger;

    public SavethemAgent(
        GameBriefingCollector briefingCollector,
        SavethemClient client,
        ILogger<SavethemAgent> logger)
    {
        _briefingCollector = briefingCollector;
        _client = client;
        _logger = logger;
    }

    public async Task<SavethemAgentResult> RunAsync(
        string searchQuery,
        string destinationCity,
        bool submitRoute = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Zbieram dane misji dla {City}...", destinationCity);
        var briefing = await _briefingCollector.CollectAsync(searchQuery, destinationCity, cancellationToken);

        var terrain = TerrainMap.FromResponse(briefing.Map);
        _logger.LogInformation(
            "Mapa {City}: start=({StartRow},{StartCol}) goal=({GoalRow},{GoalCol})",
            terrain.CityName,
            terrain.Start.Row,
            terrain.Start.Col,
            terrain.Goal.Row,
            terrain.Goal.Col);

        var route = RoutePlanner.FindBestRoute(
            terrain,
            briefing.Vehicles,
            briefing.StartingFuel,
            briefing.StartingFood)
            ?? throw new InvalidOperationException("Nie znaleziono trasy spełniającej limity paliwa i jedzenia.");

        LogRoute(route);

        JsonDocument? response = null;
        string? flag = null;

        if (submitRoute)
        {
            response = await _client.SubmitRouteAsync(route.Commands, cancellationToken);
            flag = ExtractFlag(response);
        }

        return new SavethemAgentResult
        {
            Briefing = briefing,
            Route = route,
            Response = response,
            Flag = flag,
        };
    }

    private void LogRoute(RoutePlan route)
    {
        _logger.LogInformation(
            "Plan trasy: pojazd={Vehicle}, ruchy={Moves}, paliwo={Fuel:0.0}, jedzenie={Food:0.0}",
            route.Vehicle,
            route.MoveCount,
            route.RemainingFuel,
            route.RemainingFood);

        Console.WriteLine();
        Console.WriteLine("=== Plan trasy agenta ===");
        Console.WriteLine($"Pojazd startowy : {route.Vehicle}");
        Console.WriteLine($"Liczba ruchów    : {route.MoveCount}");
        Console.WriteLine($"Paliwo po trasie : {route.RemainingFuel:0.0} / 10");
        Console.WriteLine($"Jedzenie po trasie: {route.RemainingFood:0.0} / 10");
        Console.WriteLine($"Komendy          : {string.Join(" → ", route.Commands)}");
        Console.WriteLine();
    }

    public static string? ExtractFlag(JsonDocument? response)
    {
        if (response is null)
            return null;

        var root = response.RootElement;
        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
                continue;

            var match = FlagRegex.Match(property.Value.GetString() ?? string.Empty);
            if (match.Success)
                return match.Value;
        }

        return null;
    }
}
