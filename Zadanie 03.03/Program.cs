using System.Text.Json;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using Zadanie_03._03;

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
var webhookPort = Environment.GetEnvironmentVariable("WEBHOOK_PORT");
var autoRun = string.Equals(Environment.GetEnvironmentVariable("AUTO_RUN"), "1", StringComparison.Ordinal);

IReactorStepPrompter prompter = autoRun ? new AutoStepPrompter() : new ConsoleStepPrompter();

var clientLogger = loggerFactory.CreateLogger<ReactorClient>();
var agentLogger = loggerFactory.CreateLogger<ReactorAgent>();

var webhook = new ReactorWebhook(apiKey);
var client = new ReactorClient(clientLogger, apiKey, webhook);
var agent = new ReactorAgent(client, agentLogger, prompter);

if (!string.IsNullOrWhiteSpace(webhookPort))
{
    var url = $"http://localhost:{webhookPort}";
    var app = webhook.CreateServer(url);
    _ = app.RunAsync();
    Console.WriteLine($"Webhook nasłuchuje na {url}/webhook");
}

if (autoRun)
    Console.WriteLine("Tryb AUTO_RUN — kroki wykonują się bez zatwierdzania.\n");
else
    Console.WriteLine("Tryb interaktywny — każdy krok wymaga zatwierdzenia w konsoli.\n");

Console.WriteLine("Uruchamiam agenta reaktora...\n");
var result = await agent.RunAsync();

if (result.Aborted)
{
    Console.WriteLine("\nGra przerwana przez użytkownika.");
    Console.WriteLine($"Wykonano do tej pory: {string.Join(" -> ", result.Commands)}");
    return;
}

if (result.Aborted)
{
    Console.WriteLine("\nGra przerwana przez użytkownika.");
    Console.WriteLine($"Wykonano do tej pory: {string.Join(" -> ", result.Commands)}");
    return;
}

if (result.Flag is not null)
{
    Console.WriteLine("\n=== ZADANIE ZALICZONE ===");
    Console.WriteLine($"Flaga: {result.Flag}");
}
else
{
    Console.WriteLine("\n=== Zadanie ukończone ===");
}

Console.WriteLine("\n=== Wykonane komendy ===");
Console.WriteLine(string.Join(" -> ", result.Commands));

Console.WriteLine("\n=== Stan końcowy ===");
Console.WriteLine($"Pozycja robota: kolumna {result.FinalState.PlayerCol}");
Console.WriteLine($"Rząd 5: {result.FinalState.FormatBottomRow()}");
Console.WriteLine($"Cel osiągnięty: {result.FinalState.ReachedGoal}");
Console.WriteLine($"Komunikat: {result.FinalState.Message}");

if (result.Flag is null)
{
    var flag = ReactorAgent.ExtractFlag(result.FinalState, result.LastResponse);
    if (flag is not null)
        Console.WriteLine($"Flaga: {flag}");
}

if (result.LastResponse is not null)
{
    Console.WriteLine("\n=== Odpowiedź API ===");
    Console.WriteLine(JsonSerializer.Serialize(
        result.LastResponse.RootElement,
        new JsonSerializerOptions { WriteIndented = true }));
}

Console.WriteLine("\nStan planszy zapisany w files/reactor_state.json");
