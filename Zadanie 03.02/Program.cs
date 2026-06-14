using System.ClientModel;
using System.Text.Json;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Zadanie_03._02;

Env.Load();

var apiKey = Environment.GetEnvironmentVariable("API_KEY")
    ?? throw new InvalidOperationException("Brak API_KEY w pliku .env");
var openRouterKey = Environment.GetEnvironmentVariable("OPEN_ROUTER_API_KEY")
    ?? throw new InvalidOperationException("Brak OPEN_ROUTER_API_KEY w pliku .env");
var model = Environment.GetEnvironmentVariable("OPEN_ROUTER_MODEL") ?? "openai/gpt-4o-mini";
var baseUrl = Environment.GetEnvironmentVariable("OPEN_ROUTER_BASE_URL") ?? "https://openrouter.ai/api/v1";

using var loggerFactory = LoggerFactory.Create(builder =>
    builder
        .AddSimpleConsole(o =>
        {
            o.SingleLine = false;
            o.TimestampFormat = "HH:mm:ss ";
        })
        .SetMinimumLevel(LogLevel.Information)
);

var clientLogger = loggerFactory.CreateLogger<ShellClient>();
var toolsLogger = loggerFactory.CreateLogger<ShellTools>();
var agentLogger = loggerFactory.CreateLogger<FirmwareAgent>();
var verifyLogger = loggerFactory.CreateLogger<VerifyClient>();

var client = new ShellClient(clientLogger, apiKey);
var tools = new ShellTools(client, toolsLogger);
var verifyClient = new VerifyClient(verifyLogger, apiKey);

var openAiClient = new OpenAIClient(
    new ApiKeyCredential(openRouterKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) }
);
var chatClient = openAiClient.GetChatClient(model);
var agent = new FirmwareAgent(chatClient, tools, verifyClient, agentLogger);

const string goal = """
    Spróbuj uruchomić plik binarny /opt/firmware/cooler/cooler.bin.
    Zdobądź hasło dostępowe do tej aplikacji (zapisane jest w kilku miejscach w systemie).
    Przestrzegaj zasad bezpieczeństwa.
    Na końcu wyświetl zawartość pliku cooler.bin po odszyfrowaniu.
    """;

Console.WriteLine("Uruchamiam agenta firmware...\n");
var result = await agent.RunAsync(goal);

Console.WriteLine("\n=== Wynik agenta ===\n");
Console.WriteLine(result.Reply);

if (result.VerifyResponse is not null)
{
    Console.WriteLine("\n=== Odpowiedź /verify (wysłana automatycznie) ===\n");
    Console.WriteLine(JsonSerializer.Serialize(
        result.VerifyResponse.RootElement,
        new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var eccsCode = result.EccSCode ?? EccSCode.Extract(result.Reply);
if (eccsCode is null)
{
    Console.WriteLine("\nNie znaleziono kodu ECCS — pomijam wysyłkę do /verify.");
    return;
}

Console.WriteLine($"\n=== Znaleziono kod: {eccsCode} ===");
Console.WriteLine("Wysyłam do /verify...\n");

var verifyResponse = await verifyClient.SubmitConfirmationAsync(eccsCode);
Console.WriteLine(JsonSerializer.Serialize(
    verifyResponse.RootElement,
    new JsonSerializerOptions { WriteIndented = true }));
