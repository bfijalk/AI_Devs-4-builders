using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace RailwayAgent;

public class RailwayAgentRunner
{
    private const int MaxSteps = 30;

    private const string SystemPrompt = """
        Jesteś agentem zarządzającym trasami kolejowymi przez API railway.

        Masz dostęp do następujących narzędzi:
        - help: wyświetla dostępne akcje
        - getstatus(route): pobiera aktualny status trasy
        - reconfigure(route): włącza tryb rekonfiguracji trasy
        - setstatus(route, value): ustawia status trasy (RTOPEN lub RTCLOSE) — wymaga wcześniej reconfigure
        - save(route): zapisuje zmiany i wychodzi z trybu rekonfiguracji
        - activate_route(route): automatycznie wykonuje pełną sekwencję aktywacji trasy (getstatus → reconfigure → setstatus RTOPEN → save)

        Jeśli użytkownik chce aktywować lub otworzyć trasę, użyj narzędzia activate_route — samo zadecyduje, które kroki są potrzebne.
        Narzędzie activate_route pamięta postęp — przy ponownym wywołaniu dla tej samej trasy wznowi sekwencję od nieudanego kroku.
        Jeśli chce tylko sprawdzić status lub wykonać konkretną operację, użyj odpowiedniego narzędzia bezpośrednio.

        WAŻNE: Jeśli narzędzie zwróci błąd, NIE ponawiaj samodzielnie. System przerwie działanie i zapyta użytkownika o decyzję.
        Po zakończeniu poinformuj o wyniku.
        """;

    private readonly ChatClient _chatClient;
    private readonly RailwayTools _tools;
    private readonly ILogger<RailwayAgentRunner> _logger;

    public RailwayAgentRunner(ChatClient chatClient, RailwayTools tools, ILogger<RailwayAgentRunner> logger)
    {
        _chatClient = chatClient;
        _tools = tools;
        _logger = logger;
    }

    public async Task<(string Reply, List<ChatMessage> History)> RunAsync(
        string userMessage,
        List<ChatMessage>? history = null)
    {
        var messages = history ?? [new SystemChatMessage(SystemPrompt)];
        messages.Add(new UserChatMessage(userMessage));

        var options = new ChatCompletionOptions();
        foreach (var tool in RailwayTools.GetDefinitions())
            options.Tools.Add(tool);

        for (int step = 0; step < MaxSteps; step++)
        {
            var response = await _chatClient.CompleteChatAsync(messages, options);
            var choice = response.Value;

            if (choice.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(new AssistantChatMessage(choice));

                var toolResults = new List<ToolChatMessage>();

                foreach (var toolCall in choice.ToolCalls)
                {
                    var name = toolCall.FunctionName;
                    var argsJson = toolCall.FunctionArguments.ToString();

                    string resultJson;
                    try
                    {
                        resultJson = await _tools.DispatchAsync(name, argsJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Błąd narzędzia {Name}", name);
                        resultJson = JsonSerializer.Serialize(new { error = ex.Message });
                    }

                    // Sprawdź czy wynik zawiera błąd
                    if (IsErrorResult(resultJson))
                    {
                        toolResults.Add(new ToolChatMessage(toolCall.Id, resultJson));
                        messages.AddRange(toolResults.Cast<ChatMessage>());

                        var errorReply = BuildErrorMessage(name, resultJson);
                        messages.Add(new AssistantChatMessage(errorReply));
                        return (errorReply, messages);
                    }

                    toolResults.Add(new ToolChatMessage(toolCall.Id, resultJson));
                }

                messages.AddRange(toolResults.Cast<ChatMessage>());
                continue;
            }

            var reply = choice.Content.FirstOrDefault()?.Text ?? string.Empty;
            messages.Add(new AssistantChatMessage(reply));
            return (reply, messages);
        }

        throw new InvalidOperationException($"Agent nie zakończył działania w ciągu {MaxSteps} kroków.");
    }

    private static bool IsErrorResult(string resultJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out _)) return true;
            if (root.TryGetProperty("code", out var code) && code.TryGetInt32(out var c) && c < 0) return true;
        }
        catch { /* ignoruj błędy parsowania */ }
        return false;
    }

    private static string BuildErrorMessage(string toolName, string resultJson)
    {
        string failedStep = "nieznany błąd";
        string apiMessage = string.Empty;
        string apiCode = string.Empty;
        string lastStep = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
                failedStep = err.GetString() ?? failedStep;

            var details = root.TryGetProperty("details", out var d) ? d : default;
            if (details.ValueKind == JsonValueKind.Object)
            {
                if (details.TryGetProperty("message", out var m)) apiMessage = m.GetString() ?? string.Empty;
                if (details.TryGetProperty("code", out var c)) apiCode = c.ToString();
            }
            else
            {
                if (root.TryGetProperty("message", out var m)) apiMessage = m.GetString() ?? string.Empty;
                if (root.TryGetProperty("code", out var c)) apiCode = c.ToString();
            }

            if (root.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
            {
                var last = steps.EnumerateArray().LastOrDefault();
                if (last.ValueKind == JsonValueKind.Object && last.TryGetProperty("step", out var s))
                    lastStep = s.GetString() ?? string.Empty;
            }
        }
        catch { /* ignoruj */ }

        var parts = new List<string>
        {
            $"Napotkałem problem podczas wykonywania akcji `{toolName}`.",
        };
        if (!string.IsNullOrEmpty(lastStep))
            parts.Add($"Sekwencja zatrzymała się na kroku: `{lastStep}`.");
        parts.Add($"Przyczyna błędu: {failedStep}");
        if (!string.IsNullOrEmpty(apiCode))
            parts.Add($"Kod błędu API: {apiCode}");
        if (!string.IsNullOrEmpty(apiMessage))
            parts.Add($"Komunikat API: {apiMessage}");
        parts.Add(string.Empty);
        parts.Add("Postęp został zapamiętany. Czy chcesz spróbować ponownie od tego miejsca, czy zmienić instrukcje?");

        return string.Join("\n", parts);
    }
}
