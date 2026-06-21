using System.Text.Json;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using Zadanie_03._05;

Env.Load();

using var loggerFactory = LoggerFactory.Create(builder =>
    builder
        .AddSimpleConsole(o =>
        {
            o.SingleLine = false;
            o.TimestampFormat = "HH:mm:ss ";
        })
        .SetMinimumLevel(LogLevel.Information)
);

var apiKey = Environment.GetEnvironmentVariable("API_KEY")
    ?? throw new InvalidOperationException("Brak API_KEY w pliku .env");

var client = new ToolSearchClient(loggerFactory.CreateLogger<ToolSearchClient>(), apiKey);
var briefingCollector = new GameBriefingCollector(
    client,
    loggerFactory.CreateLogger<GameBriefingCollector>());
var savethemClient = new SavethemClient(loggerFactory.CreateLogger<SavethemClient>(), apiKey);
var agent = new SavethemAgent(
    briefingCollector,
    savethemClient,
    loggerFactory.CreateLogger<SavethemAgent>());
var jsonOpts = new JsonSerializerOptions { WriteIndented = true };

var step = args.Length > 0 ? args[0] : "5";
var searchQuery = "I need notes about movement rules and terrain";
var destinationCity = Environment.GetEnvironmentVariable("DESTINATION_CITY") ?? "Skolwin";
var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);

switch (step)
{
    case "1":
    {
        var tools = await client.SearchToolsAsync(searchQuery);
        Console.WriteLine(JsonSerializer.Serialize(tools, jsonOpts));
        break;
    }

    case "2":
    {
        var map = await client.FetchTerrainMapFromSearchAsync(searchQuery, destinationCity);
        Console.WriteLine(JsonSerializer.Serialize(map, jsonOpts));
        Console.WriteLine();
        Console.WriteLine("=== Map preview ===");
        Console.WriteLine(map.Text);
        break;
    }

    case "3":
    {
        var map = await client.FetchTerrainMapFromSearchAsync(searchQuery, destinationCity);
        Console.WriteLine(TerrainMapVisualizer.RenderToConsole(map));

        var htmlPath = await TerrainMapVisualizer.SaveHtmlAsync(map);
        Console.WriteLine($"HTML preview saved to: {htmlPath}");
        break;
    }

    case "4":
    {
        var briefing = await briefingCollector.CollectAsync(searchQuery, destinationCity);
        Console.WriteLine(TerrainMapVisualizer.RenderBriefingToConsole(briefing));

        var htmlPath = await TerrainMapVisualizer.SaveBriefingHtmlAsync(briefing);
        Console.WriteLine($"Mission briefing saved to: {htmlPath}");
        break;
    }

    case "5":
    {
        var result = await agent.RunAsync(searchQuery, destinationCity, submitRoute: !dryRun);
        Console.WriteLine(TerrainMapVisualizer.RenderBriefingToConsole(result.Briefing));

        if (result.Response is not null)
        {
            Console.WriteLine("=== Odpowiedź API ===");
            Console.WriteLine(JsonSerializer.Serialize(result.Response.RootElement, jsonOpts));
        }

        if (result.Flag is not null)
        {
            Console.WriteLine();
            Console.WriteLine("=== ZADANIE ZALICZONE ===");
            Console.WriteLine($"Flaga: {result.Flag}");
        }
        else if (!dryRun)
        {
            Console.WriteLine();
            Console.WriteLine("Trasa wysłana, ale flaga nie została znaleziona w odpowiedzi.");
        }
        break;
    }

    default:
        Console.WriteLine("Usage: dotnet run [1|2|3|4|5] [--dry-run]");
        break;
}
