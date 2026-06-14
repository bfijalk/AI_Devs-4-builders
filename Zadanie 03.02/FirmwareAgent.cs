using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Zadanie_03._02;

public class FirmwareAgent
{
    private const int MaxSteps = 60;
    private const int MaxToolResultLength = 4000;

    private const string SystemPrompt = """
        Jesteś agentem eksplorującym wirtualną maszynę Linux przez narzędzia shell API.
        Używaj WYŁĄCZNIE dostępnych narzędzi shell_* — nigdy nie wymyślaj komend poza nimi.

        CEL:
        1. Spróbuj uruchomić plik binarny /opt/firmware/cooler/cooler.bin (shell_run z hasłem jako argumentem).
        2. Zdobądź hasło dostępowe do tej aplikacji — jest zapisane w kilku miejscach w systemie.
        3. Na końcu wyświetl zawartość pliku cooler.bin po odszyfrowaniu.
           Odszyfrowana treść pochodzi z pola data odpowiedzi po udanym shell_run — NIE używaj shell_cat na cooler.bin.

        ZASADY BEZPIECZEŃSTWA (KRYTYCZNE — złamanie skutkuje banem API i resetem VM):
        - Pracujesz na koncie zwykłego użytkownika.
        - NIGDY nie zaglądaj do katalogów /etc, /root i /proc/.
        - Jeśli w katalogu znajdziesz plik .gitignore, najpierw go przeczytaj (shell_cat) i respektuj go.
          Nie dotykaj plików ani katalogów wymienionych w .gitignore.

        Każde narzędzie zwraca pełną odpowiedź JSON (code, message, data).
        Jeśli code < 0 — zmień strategię i spróbuj inaczej.
        Gdy w odpowiedzi shell_run pojawi się kod ECCS-xxxxxxxx..., ZAKOŃCZ pracę — nie wykonuj więcej komend.
        Kod ECCS to potwierdzenie do wysłania do centrali, NIE jest hasłem do cooler.bin.
        """;

    private readonly ChatClient _chatClient;
    private readonly ShellTools _tools;
    private readonly VerifyClient _verifyClient;
    private readonly ILogger<FirmwareAgent> _logger;

    public FirmwareAgent(
        ChatClient chatClient,
        ShellTools tools,
        VerifyClient verifyClient,
        ILogger<FirmwareAgent> logger)
    {
        _chatClient = chatClient;
        _tools = tools;
        _verifyClient = verifyClient;
        _logger = logger;
    }

    public async Task<FirmwareAgentResult> RunAsync(
        string userMessage,
        List<ChatMessage>? history = null)
    {
        var messages = history ?? [new SystemChatMessage(SystemPrompt)];
        messages.Add(new UserChatMessage(userMessage));

        var options = new ChatCompletionOptions();
        foreach (var tool in ShellTools.GetToolDefinitions())
            options.Tools.Add(tool);

        string? eccsCode = null;
        string? decryptedOutput = null;

        for (int step = 0; step < MaxSteps; step++)
        {
            _logger.LogInformation("Krok agenta {Step}/{Max}", step + 1, MaxSteps);

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

                    var sanitized = SanitizeToolResult(resultJson);
                    _logger.LogInformation("Wynik {Name}: {Result}", name, Truncate(sanitized, 500));
                    toolResults.Add(new ToolChatMessage(toolCall.Id, sanitized));

                    var foundCode = EccSCode.Extract(resultJson);
                    if (foundCode is null)
                        continue;

                    eccsCode = foundCode;
                    decryptedOutput ??= TryExtractDecryptedOutput(resultJson);

                    _logger.LogInformation("Znaleziono kod ECCS — kończę pracę i wysyłam do /verify.");
                    var verifyResponse = await _verifyClient.SubmitConfirmationAsync(foundCode);
                    var summary = BuildSuccessSummary(decryptedOutput, foundCode);
                    return new FirmwareAgentResult(summary, messages, foundCode, decryptedOutput, verifyResponse);
                }

                messages.AddRange(toolResults.Cast<ChatMessage>());
                continue;
            }

            var reply = choice.Content.FirstOrDefault()?.Text ?? string.Empty;
            eccsCode ??= EccSCode.Extract(reply);
            messages.Add(new AssistantChatMessage(reply));
            return new FirmwareAgentResult(reply, messages, eccsCode, decryptedOutput);
        }

        throw new InvalidOperationException($"Agent nie zakończył działania w ciągu {MaxSteps} kroków.");
    }

    private static string? TryExtractDecryptedOutput(string resultJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() == 196
                && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
            {
                return data.GetString();
            }
        }
        catch { /* ignoruj */ }

        return null;
    }

    private static string BuildSuccessSummary(string? decryptedOutput, string eccsCode)
    {
        var parts = new List<string>
        {
            "cooler.bin uruchomiony pomyślnie.",
            "",
            "=== Zawartość cooler.bin po odszyfrowaniu ===",
            decryptedOutput ?? "(brak pola data w odpowiedzi)",
            "",
            $"=== Kod potwierdzenia do centrali ===",
            eccsCode,
        };
        return string.Join('\n', parts);
    }

    private static string SanitizeToolResult(string resultJson)
    {
        var cleaned = resultJson.Replace("\\u0000", "", StringComparison.Ordinal);

        if (cleaned.Length > MaxToolResultLength)
            cleaned = cleaned[..MaxToolResultLength] + "...[TRUNCATED]";

        return cleaned;
    }

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "...";
}

public record FirmwareAgentResult(
    string Reply,
    List<ChatMessage> History,
    string? EccSCode,
    string? DecryptedOutput = null,
    JsonDocument? VerifyResponse = null);
