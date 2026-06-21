using Microsoft.Extensions.Logging;

namespace Zadanie_03._05;

public class GameBriefingCollector
{
    private static readonly string[] VehicleNames = ["walk", "horse", "car", "rocket"];
    private static readonly string[] MovementQueries =
    [
        "movement rules terrain legend water",
        "vehicle selection dismount fuel food",
        "savethem API commands orientation",
    ];

    private readonly ToolSearchClient _client;
    private readonly ILogger<GameBriefingCollector> _logger;

    public GameBriefingCollector(ToolSearchClient client, ILogger<GameBriefingCollector> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<GameBriefing> CollectAsync(
        string searchQuery,
        string destinationCity,
        CancellationToken cancellationToken = default)
    {
        var tools = await _client.SearchToolsAsync(searchQuery, cancellationToken);
        var vehicleTools = await _client.SearchToolsAsync("vehicles fuel food transportation", cancellationToken);

        var allTools = tools.Tools
            .Concat(vehicleTools.Tools)
            .GroupBy(t => t.Name)
            .Select(g => g.First())
            .ToList();

        if (allTools.Any(t => t.Name is "wehicles" or "vehicles"))
            _logger.LogInformation("Vehicles tool found via toolsearch");

        var map = await _client.FetchTerrainMapAsync(destinationCity, cancellationToken);
        var vehicles = await FetchAllVehiclesAsync(cancellationToken);
        var notes = await FetchMovementNotesAsync(cancellationToken);

        return new GameBriefing(
            DestinationCity: destinationCity,
            StartingFood: 10,
            StartingFuel: 10,
            Map: map,
            Vehicles: vehicles,
            MovementNotes: notes,
            AvailableTools: allTools);
    }

    public async Task<IReadOnlyList<VehicleInfo>> FetchAllVehiclesAsync(
        CancellationToken cancellationToken = default)
    {
        var vehicles = new List<VehicleInfo>();

        foreach (var name in VehicleNames)
        {
            var info = await _client.FetchVehicleAsync(name, cancellationToken);
            vehicles.Add(info);
            _logger.LogInformation(
                "Vehicle {Name}: fuel={Fuel}/move, food={Food}/move",
                info.Name, info.Consumption.Fuel, info.Consumption.Food);
        }

        return vehicles;
    }

    private async Task<IReadOnlyList<BookNote>> FetchMovementNotesAsync(
        CancellationToken cancellationToken)
    {
        var notesById = new Dictionary<string, BookNote>();

        foreach (var query in MovementQueries)
        {
            var response = await _client.FetchBooksAsync(query, cancellationToken);
            foreach (var note in response.Notes)
                notesById.TryAdd(note.Id, note);
        }

        return notesById.Values
            .OrderByDescending(n => n.Score)
            .ToList();
    }
}

public record GameBriefing(
    string DestinationCity,
    int StartingFood,
    int StartingFuel,
    TerrainMapResponse Map,
    IReadOnlyList<VehicleInfo> Vehicles,
    IReadOnlyList<BookNote> MovementNotes,
    IReadOnlyList<AgentToolInfo> AvailableTools);

public record VehicleInfo(
    string Name,
    string Note,
    VehicleConsumption Consumption);

public record VehicleConsumption(
    double Fuel,
    double Food);
