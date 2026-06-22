using System.ClientModel;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Zadanie_04._01;

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

var step = args.Length > 0 ? args[0] : "1";
var exitCode = 0;

switch (step)
{
    case "1":
    {
        var client = new OkoEditorClient(loggerFactory.CreateLogger<OkoEditorClient>(), apiKey);
        var outputPath = await OkoEditorApiDocumenter.DocumentAsync(client);
        Console.WriteLine($"Dokumentacja zapisana w: {outputPath}");
        break;
    }

    case "2":
    {
        await using var webAgent = CreateWebAgent(loggerFactory, apiKey);
        await webAgent.LoginAsync();
        var entries = await webAgent.DiscoverEntriesAsync();

        Console.WriteLine();
        Console.WriteLine("=== Dostępne raporty ===");
        foreach (var entry in entries)
        {
            Console.WriteLine($"- [{entry.Section}] {entry.Title}");
        }

        Console.WriteLine();
        Console.Write("Podaj nazwę lub fragment raportu do pobrania: ");
        var query = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("Nie podano zapytania o raport.");
        }

        var match = webAgent.FindBestMatch(query, entries)
            ?? throw new InvalidOperationException(
                $"Nie znaleziono raportu pasującego do zapytania: \"{query}\"");

        var report = await webAgent.FetchReportAsync(match);
        var context = OkoReportContext.From(match, report, entries);

        Console.WriteLine();
        Console.WriteLine("=== Raport pobrany ===");
        Console.WriteLine($"Tytuł: {report.Title}");
        Console.WriteLine($"Sekcja: {report.Section}");
        Console.WriteLine($"Markdown: {report.MarkdownPath}");

        var interactiveAgent = await CreateInteractiveAgentAsync(webAgent, apiKey, loggerFactory);
        interactiveAgent.SetCatalog(entries);
        await interactiveAgent.RunAsync(context);
        break;
    }

    case "3":
    {
        await using var webAgent = CreateWebAgent(loggerFactory, apiKey);
        var interactiveAgent = await CreateInteractiveAgentAsync(webAgent, apiKey, loggerFactory);
        await interactiveAgent.RunAsync();
        break;
    }

    case "4":
    {
        await using var webAgent = CreateWebAgent(loggerFactory, apiKey);
        var autonomousAgent = CreateAutonomousAgent(webAgent, apiKey, loggerFactory);
        if (!await autonomousAgent.RunAsync())
        {
            exitCode = 1;
        }

        break;
    }

    default:
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run 1   — dokumentacja API");
        Console.WriteLine("  dotnet run 2   — pobranie raportu i praca nad zmianami");
        Console.WriteLine("  dotnet run 3   — pełny interaktywny agent (menu główne)");
        Console.WriteLine("  dotnet run 4   — tryb autonomiczny (naprawa + done)");
        Console.WriteLine();
        Console.WriteLine("Przy pierwszym uruchomieniu kroku 2/3/4 Playwright pobierze Chromium automatycznie.");
        break;
}

Environment.Exit(exitCode);

static OkoWebAgent CreateWebAgent(ILoggerFactory loggerFactory, string apiKey)
    => new(
        loggerFactory.CreateLogger<OkoWebAgent>(),
        login: "Zofia",
        password: "Zofia2026!",
        accessKey: apiKey);

static async Task<OkoEditorInteractiveAgent> CreateInteractiveAgentAsync(
    OkoWebAgent webAgent,
    string apiKey,
    ILoggerFactory loggerFactory)
{
    var apiDocs = await LoadApiDocsAsync(apiKey, loggerFactory);
    var apiClient = new OkoEditorClient(loggerFactory.CreateLogger<OkoEditorClient>(), apiKey);
    var tools = new OkoEditorAgentTools(
        apiClient,
        webAgent,
        loggerFactory.CreateLogger<OkoEditorAgentTools>());

    var llmAgent = new OkoEditorLlmAgent(
        CreateChatClient(),
        tools,
        loggerFactory.CreateLogger<OkoEditorLlmAgent>(),
        apiDocs);

    return new OkoEditorInteractiveAgent(
        webAgent,
        tools,
        llmAgent,
        CreateAutonomousAgent(webAgent, apiKey, loggerFactory),
        loggerFactory.CreateLogger<OkoEditorInteractiveAgent>());
}

static OkoAutonomousAgent CreateAutonomousAgent(
    OkoWebAgent webAgent,
    string apiKey,
    ILoggerFactory loggerFactory)
{
    var apiClient = new OkoEditorClient(loggerFactory.CreateLogger<OkoEditorClient>(), apiKey);
    var tools = new OkoEditorAgentTools(
        apiClient,
        webAgent,
        loggerFactory.CreateLogger<OkoEditorAgentTools>());
    var repair = new OkoMissionRepair(apiClient, loggerFactory.CreateLogger<OkoMissionRepair>());
    var completionLoop = new OkoDoneCompletionLoop(
        tools,
        repair,
        loggerFactory.CreateLogger<OkoDoneCompletionLoop>());

    return new OkoAutonomousAgent(
        webAgent,
        tools,
        completionLoop,
        loggerFactory.CreateLogger<OkoAutonomousAgent>());
}

static async Task<string> LoadApiDocsAsync(string apiKey, ILoggerFactory loggerFactory)
{
    var apiDocsPath = Path.Combine("files", "okoeditor_api.md");
    if (!File.Exists(apiDocsPath))
    {
        var client = new OkoEditorClient(loggerFactory.CreateLogger<OkoEditorClient>(), apiKey);
        await OkoEditorApiDocumenter.DocumentAsync(client);
    }

    return await File.ReadAllTextAsync(apiDocsPath);
}

static ChatClient CreateChatClient()
{
    var openRouterKey = Environment.GetEnvironmentVariable("OPEN_ROUTER_API_KEY")
        ?? throw new InvalidOperationException("Brak OPEN_ROUTER_API_KEY w pliku .env");
    var model = Environment.GetEnvironmentVariable("OPEN_ROUTER_MODEL") ?? "openai/gpt-4o-mini";
    var baseUrl = Environment.GetEnvironmentVariable("OPEN_ROUTER_BASE_URL")
        ?? "https://openrouter.ai/api/v1";

    var openAiClient = new OpenAIClient(
        new ApiKeyCredential(openRouterKey),
        new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

    return openAiClient.GetChatClient(model);
}
