using DotNetEnv;
using Microsoft.Extensions.Logging;
using Zadanie_04._02;

Env.Load();

var verbose = args.Contains("--verbose", StringComparer.OrdinalIgnoreCase);
var step = args.FirstOrDefault(arg => arg is not "--verbose") ?? "3";

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddSimpleConsole(o =>
        {
            o.SingleLine = false;
            o.TimestampFormat = "HH:mm:ss ";
        })
        .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);

    if (!verbose)
    {
        builder.AddFilter("Zadanie_04._02.WindPowerAgentTools", LogLevel.Warning);
    }
});

var apiKey = Environment.GetEnvironmentVariable("API_KEY")
    ?? throw new InvalidOperationException("Brak API_KEY w pliku .env");

switch (step)
{
    case "1":
    {
        var client = new WindPowerClient(loggerFactory.CreateLogger<WindPowerClient>(), apiKey);
        using var help = await client.GetHelpAsync();
        Console.WriteLine("=== Windpower API — help ===");
        Console.WriteLine(WindPowerClient.FormatResponse(help));
        break;
    }

    case "2":
    {
        var client = new WindPowerClient(loggerFactory.CreateLogger<WindPowerClient>(), apiKey);
        var tools = new WindPowerAgentTools(
            client,
            loggerFactory.CreateLogger<WindPowerAgentTools>());

        Console.WriteLine("=== Zarejestrowane narzędzia agenta ===");
        foreach (var tool in WindPowerAgentTools.GetToolDefinitions())
        {
            Console.WriteLine($"- {tool.Name}: {tool.Description}");
        }

        Console.WriteLine();
        Console.WriteLine("=== start ===");
        Console.WriteLine(await tools.StartAsync());

        Console.WriteLine();
        Console.WriteLine("=== równoległe get: documentation + weather + turbinecheck ===");
        var parallelResult = await tools.DispatchParallelAsync(
        [
            new WindPowerToolCall("windpower_get", """{"param":"documentation"}"""),
            new WindPowerToolCall("windpower_get", """{"param":"weather"}"""),
            new WindPowerToolCall("windpower_get", """{"param":"turbinecheck"}"""),
        ]);
        Console.WriteLine(parallelResult);

        Console.WriteLine();
        Console.WriteLine("=== collect_results (kolejka asynchroniczna) ===");
        Console.WriteLine(await tools.CollectQueuedResultsAsync());
        break;
    }

    case "3":
    {
        var client = new WindPowerClient(loggerFactory.CreateLogger<WindPowerClient>(), apiKey);
        var agent = new WindPowerAgent(client, loggerFactory.CreateLogger<WindPowerAgent>());
        var result = await agent.RunAsync();

        Console.WriteLine();
        if (result.Success && result.Flag is not null)
        {
            Console.WriteLine("=== ZADANIE ZALICZONE ===");
            Console.WriteLine($"Flaga: {result.Flag}");
        }
        else
        {
            Console.WriteLine("=== BŁĄD ===");
            Console.WriteLine(result.Error ?? "Nie udało się uzyskać flagi.");
            Environment.Exit(1);
        }

        break;
    }

    default:
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run 1   — sprawdzenie możliwości API (action: help)");
        Console.WriteLine("  dotnet run 2   — demonstracja narzędzi agenta (równoległe wywołania)");
        Console.WriteLine("  dotnet run 3   — agent rozwiązujący zadanie windpower (domyślny)");
        Console.WriteLine("  dotnet run 3 --verbose   — pełne logi HTTP i debug");
        break;
}
