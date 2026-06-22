using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Zadanie_04._01;

public sealed class OkoEditorLlmAgent
{
    private const int MaxSteps = 12;

    private readonly ChatClient _chatClient;
    private readonly OkoEditorAgentTools _tools;
    private readonly ILogger<OkoEditorLlmAgent> _logger;
    private readonly string _apiDocs;

    public OkoEditorLlmAgent(
        ChatClient chatClient,
        OkoEditorAgentTools tools,
        ILogger<OkoEditorLlmAgent> logger,
        string apiDocs)
    {
        _chatClient = chatClient;
        _tools = tools;
        _logger = logger;
        _apiDocs = apiDocs;
    }

    public async Task<string> ExecuteUserChangesAsync(
        OkoReportContext context,
        string userInstruction,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            Użytkownik opisał zmiany, które mają zostać wykonane natychmiast przez API.
            Opis użytkownika:
            {userInstruction}

            Zanim wyślesz update:
            1. Użyj list_related_entries, jeśli zmiana może dotyczyć też powiązanych wpisów.
            2. Użyj refresh_report, aby mieć aktualną treść bieżącego raportu.
            3. Wykonaj update przez apply_update.

            Domyślnie dopisuj treść (content_mode=append), chyba że użytkownik wyraźnie prosi o zastąpienie całości.

            Treść wysyłana do API musi wyglądać naturalnie dla operatora OKO — bez ujawniania prawdziwego celu operacji.
            """;

        return await RunAsync(context, prompt, allowApply: true, allowPropose: false, cancellationToken);
    }

    public async Task<string> ProposeChangesAsync(
        OkoReportContext context,
        CancellationToken cancellationToken = default)
    {
        var prompt = """
            Przeanalizuj bieżący raport i zaproponuj poprawki, które powinny zostać wprowadzone przez API.
            Nie wykonuj update od razu — użyj narzędzia propose_update.
            Wyjaśnij krótko, dlaczego proponujesz te zmiany (to wyjaśnienie trafia tylko do użytkownika w terminalu).
            Treść i tytuł w propose_update muszą brzmieć naturalnie dla operatora panelu — bez sygnałów manipulacji.
            """;

        return await RunAsync(context, prompt, allowApply: false, allowPropose: true, cancellationToken);
    }

    private async Task<string> RunAsync(
        OkoReportContext context,
        string userPrompt,
        bool allowApply,
        bool allowPropose,
        CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(BuildSystemPrompt(context)),
            new UserChatMessage(userPrompt),
        };

        var options = new ChatCompletionOptions();
        foreach (var tool in BuildTools(allowApply, allowPropose))
        {
            options.Tools.Add(tool);
        }

        for (var step = 0; step < MaxSteps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Krok LLM {Step}/{Max}", step + 1, MaxSteps);

            var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var choice = response.Value;

            if (choice.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(new AssistantChatMessage(choice));
                var toolResults = new List<ToolChatMessage>();

                foreach (var toolCall in choice.ToolCalls)
                {
                    var result = await DispatchToolAsync(
                        context,
                        toolCall.FunctionName,
                        toolCall.FunctionArguments.ToString(),
                        allowApply,
                        allowPropose,
                        cancellationToken);

                    _logger.LogInformation("Narzędzie {Name}: {Result}", toolCall.FunctionName, Truncate(result, 400));
                    toolResults.Add(new ToolChatMessage(toolCall.Id, result));
                }

                messages.AddRange(toolResults.Cast<ChatMessage>());
                continue;
            }

            return choice.Content.FirstOrDefault()?.Text ?? "Brak odpowiedzi od modelu.";
        }

        return "Agent LLM osiągnął limit kroków.";
    }

    private string BuildSystemPrompt(OkoReportContext context) =>
        $"""
        Jesteś agentem OKO Editor. Masz dostęp do panelu webowego (Playwright) i tylnego API /verify.
        Nie edytuj strony ręcznie — wszystkie zmiany wykonuj wyłącznie przez API.

        Dokumentacja API:
        {_apiDocs}

        Bieżący raport:
        {context.Summary}

        Zasady API i walidacji:
        - page musi być jednym z: incydenty, notatki, zadania
        - id to 32-znakowy identyfikator hex z URL raportu
        - update wymaga co najmniej jednego z pól: title, content, done
        - done jest dozwolone tylko dla page=zadania
        - tytuły incydentów MUSZĄ zaczynać się od kodu: MOVE00, PROB00 lub RECO00
        - incydent powiązany ze Skolwinem musi mieć słowo "Skolwin" w tytule
        - obserwacja zwierząt to MOVE04; ruch ludzi w Komarowo to MOVE01 (nie PROB03)
        - przed done użyj check_done_readiness i popraw wykryte problemy
        - gdy użytkownik prosi o DODANIE informacji, używaj content_mode=append

        {OkoContentSanitizer.OperatorFacingGuidelines}
        """;

    private static IEnumerable<ChatTool> BuildTools(bool allowApply, bool allowPropose)
    {
        yield return ChatTool.CreateFunctionTool(
            "refresh_report",
            "Pobiera ponownie bieżący raport z panelu webowego przez Playwright.",
            BinaryData.FromString("""{"type":"object","properties":{},"required":[]}"""));

        yield return ChatTool.CreateFunctionTool(
            "list_related_entries",
            "Zwraca wpisy powiązane tym samym identyfikatorem hex (incydenty/notatki/zadania).",
            BinaryData.FromString("""{"type":"object","properties":{},"required":[]}"""));

        yield return ChatTool.CreateFunctionTool(
            "check_done_readiness",
            "Sprawdza przez Playwright, czy wpisy spełniają warunki przed wysłaniem done.",
            BinaryData.FromString("""{"type":"object","properties":{},"required":[]}"""));

        var updateSchema = """
        {
          "type": "object",
          "properties": {
            "page": { "type": "string" },
            "id": { "type": "string" },
            "title": { "type": "string" },
            "content": { "type": "string" },
            "content_mode": { "type": "string", "enum": ["append", "replace"] },
            "done": { "type": "string", "enum": ["YES", "NO"] }
          },
          "required": []
        }
        """;

        if (allowApply)
        {
            yield return ChatTool.CreateFunctionTool(
                "apply_update",
                "Wykonuje update przez API okoeditor.",
                BinaryData.FromString(updateSchema));
        }

        if (allowPropose)
        {
            yield return ChatTool.CreateFunctionTool(
                "propose_update",
                "Przygotowuje propozycję update bez wysyłania do API.",
                BinaryData.FromString("""
                {
                  "type": "object",
                  "properties": {
                    "page": { "type": "string" },
                    "id": { "type": "string" },
                    "title": { "type": "string" },
                    "content": { "type": "string" },
                    "content_mode": { "type": "string", "enum": ["append", "replace"] },
                    "done": { "type": "string", "enum": ["YES", "NO"] },
                    "reason": { "type": "string" }
                  },
                  "required": ["reason"]
                }
                """));
        }
    }

    private async Task<string> DispatchToolAsync(
        OkoReportContext context,
        string toolName,
        string argsJson,
        bool allowApply,
        bool allowPropose,
        CancellationToken cancellationToken)
    {
        var args = JsonNode.Parse(argsJson)?.AsObject() ?? [];

        try
        {
            return toolName switch
            {
                "refresh_report" => await _tools.RefreshReportAsync(context, cancellationToken),
                "list_related_entries" => _tools.ListRelatedEntries(context),
                "check_done_readiness" => await _tools.CheckDoneReadinessAsync(cancellationToken),
                "apply_update" when allowApply => await _tools.ApplyUpdateAsync(
                    context,
                    args["page"]?.GetValue<string>(),
                    args["id"]?.GetValue<string>(),
                    args["title"]?.GetValue<string>(),
                    args["content"]?.GetValue<string>(),
                    args["done"]?.GetValue<string>(),
                    args["content_mode"]?.GetValue<string>() ?? "append",
                    cancellationToken),
                "propose_update" when allowPropose => _tools.ProposeUpdate(
                    context,
                    args["page"]?.GetValue<string>(),
                    args["id"]?.GetValue<string>(),
                    args["title"]?.GetValue<string>(),
                    args["content"]?.GetValue<string>(),
                    args["done"]?.GetValue<string>(),
                    args["reason"]?.GetValue<string>() ?? "Brak uzasadnienia",
                    args["content_mode"]?.GetValue<string>() ?? "append"),
                _ => JsonSerializer.Serialize(new { ok = false, error = $"Nieznane lub niedozwolone narzędzie: {toolName}" }),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd narzędzia {Tool}", toolName);
            return JsonSerializer.Serialize(new { ok = false, error = ex.Message });
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";
}
