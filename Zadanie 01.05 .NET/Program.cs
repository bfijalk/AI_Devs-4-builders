using System.ClientModel;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using RailwayAgent;

Env.Load();

var apiKey       = Environment.GetEnvironmentVariable("API_KEY")             ?? throw new Exception("Brak API_KEY w .env");
var openRouterKey = Environment.GetEnvironmentVariable("OPEN_ROUTER_API_KEY") ?? throw new Exception("Brak OPEN_ROUTER_API_KEY w .env");
var model        = Environment.GetEnvironmentVariable("OPEN_ROUTER_MODEL")   ?? "gpt-4o";
var baseUrl      = Environment.GetEnvironmentVariable("OPEN_ROUTER_BASE_URL") ?? "https://openrouter.ai/api/v1";

using var loggerFactory = LoggerFactory.Create(builder =>
    builder
        .AddSimpleConsole(o =>
        {
            o.SingleLine = false;
            o.TimestampFormat = "HH:mm:ss ";
        })
        .SetMinimumLevel(LogLevel.Debug)
);

var clientLogger = loggerFactory.CreateLogger<RailwayClient>();
var toolsLogger  = loggerFactory.CreateLogger<RailwayTools>();
var agentLogger  = loggerFactory.CreateLogger<RailwayAgentRunner>();

var railwayClient = new RailwayClient(clientLogger, apiKey);
var tools         = new RailwayTools(railwayClient, toolsLogger);

var openAiClient  = new OpenAIClient(
    new ApiKeyCredential(openRouterKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) }
);
var chatClient = openAiClient.GetChatClient(model);
var agent      = new RailwayAgentRunner(chatClient, tools, agentLogger);

Console.WriteLine("Agent Railway — zarządzanie trasami kolejowymi");
Console.WriteLine("Wpisz 'exit' lub 'quit' aby zakończyć.\n");

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nDo widzenia!");
    Environment.Exit(0);
};

List<ChatMessage>? history = null;

while (true)
{
    Console.Write("Ty: ");

    string? input;
    try
    {
        input = Console.ReadLine()?.Trim();
    }
    catch (Exception)
    {
        Console.WriteLine("\nDo widzenia!");
        break;
    }

    // EOF (Ctrl+D / potok zamknięty)
    if (input is null)
    {
        Console.WriteLine("\nDo widzenia!");
        break;
    }

    if (string.IsNullOrEmpty(input)) continue;

    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)
     || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Do widzenia!");
        break;
    }

    try
    {
        var (reply, updatedHistory) = await agent.RunAsync(input, history);
        history = updatedHistory;
        Console.WriteLine($"\nAgent: {reply}\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[Błąd]: {ex.Message}\n");
    }
}
