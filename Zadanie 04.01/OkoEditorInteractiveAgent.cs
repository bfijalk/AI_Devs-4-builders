using Microsoft.Extensions.Logging;

namespace Zadanie_04._01;

public sealed class OkoEditorInteractiveAgent
{
    private readonly OkoWebAgent _webAgent;
    private readonly OkoEditorAgentTools _tools;
    private readonly OkoEditorLlmAgent _llmAgent;
    private readonly OkoAutonomousAgent _autonomousAgent;
    private readonly ILogger<OkoEditorInteractiveAgent> _logger;

    public OkoEditorInteractiveAgent(
        OkoWebAgent webAgent,
        OkoEditorAgentTools tools,
        OkoEditorLlmAgent llmAgent,
        OkoAutonomousAgent autonomousAgent,
        ILogger<OkoEditorInteractiveAgent> logger)
    {
        _webAgent = webAgent;
        _tools = tools;
        _llmAgent = llmAgent;
        _autonomousAgent = autonomousAgent;
        _logger = logger;
    }

    public void SetCatalog(IReadOnlyList<OkoWebEntry> catalog)
        => _tools.SetCatalog(catalog);

    public async Task RunAsync(
        OkoReportContext? initialContext = null,
        CancellationToken cancellationToken = default)
    {
        await _webAgent.LoginAsync(cancellationToken);
        IReadOnlyList<OkoWebEntry>? catalog = null;
        OkoReportContext? current = initialContext;

        if (current is not null)
        {
            PrintLoadedReport(current);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            if (current is null)
            {
                var mainChoice = PromptMainMenu();
                switch (mainChoice)
                {
                    case "1":
                        catalog ??= await _webAgent.DiscoverEntriesAsync(cancellationToken);
                        _tools.SetCatalog(catalog);
                        current = await SelectReportAsync(catalog, cancellationToken);
                        break;
                    case "2":
                        if (await SendDoneAsync(cancellationToken))
                        {
                            return;
                        }

                        break;
                    case "3":
                        await CheckDoneReadinessAsync(cancellationToken);
                        break;
                    case "4":
                        if (await RunAutonomousAsync(cancellationToken))
                        {
                            return;
                        }

                        break;
                    case "0":
                        Console.WriteLine("Do widzenia.");
                        return;
                    default:
                        Console.WriteLine("Nieprawidłowa opcja.");
                        break;
                }

                continue;
            }

            var reportChoice = PromptReportMenu(current);
            switch (reportChoice)
            {
                case "1":
                    await HandleUserChangesAsync(current, cancellationToken);
                    break;
                case "2":
                    await HandleLlmProposalAsync(current, cancellationToken);
                    break;
                case "3":
                    await HandleApprovePendingAsync(current, cancellationToken);
                    break;
                case "4":
                    current = null;
                    Console.WriteLine("Wrócono do menu głównego.");
                    break;
                case "5":
                    if (await SendDoneAsync(cancellationToken))
                    {
                        return;
                    }

                    break;
                case "6":
                    await CheckDoneReadinessAsync(cancellationToken);
                    break;
                case "0":
                    Console.WriteLine("Do widzenia.");
                    return;
                default:
                    Console.WriteLine("Nieprawidłowa opcja.");
                    break;
            }
        }
    }

    private async Task<OkoReportContext?> SelectReportAsync(
        IReadOnlyList<OkoWebEntry> catalog,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("=== Dostępne raporty ===");
        foreach (var entry in catalog)
        {
            Console.WriteLine($"- [{entry.Section}] {entry.Title}");
        }

        Console.WriteLine();
        Console.Write("Podaj nazwę lub fragment raportu: ");
        var query = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(query))
        {
            Console.WriteLine("Nie podano zapytania.");
            return null;
        }

        var match = _webAgent.FindBestMatch(query, catalog);
        if (match is null)
        {
            Console.WriteLine($"Nie znaleziono raportu pasującego do: \"{query}\"");
            return null;
        }

        var report = await _webAgent.FetchReportAsync(match, cancellationToken);
        var context = OkoReportContext.From(match, report, catalog);

        Console.WriteLine();
        Console.WriteLine("=== Raport wczytany ===");
        Console.WriteLine($"Tytuł: {report.Title}");
        Console.WriteLine($"Sekcja: {report.Section}");
        Console.WriteLine($"Page/Id: {context.Page}/{context.Id}");
        Console.WriteLine($"Plik: {report.MarkdownPath}");

        return context;
    }

    private static void PrintLoadedReport(OkoReportContext context)
    {
        Console.WriteLine();
        Console.WriteLine("=== Raport wczytany ===");
        Console.WriteLine($"Tytuł: {context.Report.Title}");
        Console.WriteLine($"Sekcja: {context.Report.Section}");
        Console.WriteLine($"Page/Id: {context.Page}/{context.Id}");
        Console.WriteLine($"Plik: {context.Report.MarkdownPath}");
        Console.WriteLine();
        Console.WriteLine("Możesz teraz pracować nad zmianami tego raportu.");
    }

    private async Task HandleUserChangesAsync(
        OkoReportContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.Write("Opisz zmiany, które mają zostać wykonane: ");
        var instruction = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(instruction))
        {
            Console.WriteLine("Nie podano opisu zmian.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Agent analizuje i wykonuje zmiany...");
        var reply = await _llmAgent.ExecuteUserChangesAsync(context, instruction, cancellationToken);
        Console.WriteLine();
        Console.WriteLine(reply);

        await RefreshCurrentReportAsync(context, cancellationToken);
    }

    private async Task HandleLlmProposalAsync(
        OkoReportContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("Agent przygotowuje propozycję zmian...");
        var reply = await _llmAgent.ProposeChangesAsync(context, cancellationToken);
        Console.WriteLine();
        Console.WriteLine(reply);

        if (context.PendingUpdate is not null)
        {
            Console.WriteLine();
            Console.WriteLine("=== Propozycja LLM ===");
            Console.WriteLine(OkoEditorAgentTools.FormatPendingUpdate(context.PendingUpdate));
            Console.WriteLine("Użyj opcji 3, aby zatwierdzić te zmiany.");
        }
    }

    private async Task HandleApprovePendingAsync(
        OkoReportContext context,
        CancellationToken cancellationToken)
    {
        if (context.PendingUpdate is null)
        {
            Console.WriteLine("Brak oczekujących zmian od LLM.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("=== Zatwierdzane zmiany ===");
        Console.WriteLine(OkoEditorAgentTools.FormatPendingUpdate(context.PendingUpdate));
        Console.Write("Zatwierdzić i wysłać do API? (t/n): ");
        var confirm = Console.ReadLine();

        if (!IsYes(confirm))
        {
            Console.WriteLine("Anulowano.");
            return;
        }

        var response = await _tools.ApprovePendingUpdateAsync(context, cancellationToken);
        Console.WriteLine();
        Console.WriteLine(response);
        PrintApiResult(response);

        await RefreshCurrentReportAsync(context, cancellationToken);
    }

    private async Task<bool> RunAutonomousAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("Uruchamiam tryb autonomiczny...");
        return await _autonomousAgent.RunAsync(cancellationToken);
    }

    private async Task CheckDoneReadinessAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("Sprawdzam gotowość do done...");
        var response = await _tools.CheckDoneReadinessAsync(cancellationToken);
        Console.WriteLine();
        Console.WriteLine(response);
        PrintApiResult(response);
    }

    private async Task<bool> SendDoneAsync(CancellationToken cancellationToken)
    {
        Console.Write("Wysłać request done do API? (t/n): ");
        var confirm = Console.ReadLine();
        if (!IsYes(confirm))
        {
            Console.WriteLine("Anulowano.");
            return false;
        }

        var response = await _tools.SendDoneAsync(cancellationToken);
        Console.WriteLine();
        Console.WriteLine(response);
        return PrintApiResult(response);
    }

    private async Task RefreshCurrentReportAsync(
        OkoReportContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await _webAgent.FetchReportAsync(context.Entry, cancellationToken);
            context.Report = report;
            _logger.LogInformation("Odświeżono raport: {Title}", report.Title);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się odświeżyć raportu po zmianie.");
        }
    }

    private static bool PrintApiResult(string response)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("ok", out var okProp) &&
                okProp.ValueKind == System.Text.Json.JsonValueKind.False)
            {
                Console.WriteLine();
                Console.WriteLine("=== Błąd API ===");
                if (doc.RootElement.TryGetProperty("message", out var message))
                {
                    Console.WriteLine(message.GetString());
                }

                if (doc.RootElement.TryGetProperty("hint", out var hint))
                {
                    Console.WriteLine($"Podpowiedź: {hint.GetString()}");
                }

                if (doc.RootElement.TryGetProperty("violations", out var violations) &&
                    violations.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in violations.EnumerateArray())
                    {
                        Console.WriteLine($"- {item.GetString()}");
                    }
                }

                if (doc.RootElement.TryGetProperty("summary", out var summary))
                {
                    Console.WriteLine();
                    Console.WriteLine(summary.GetString());
                }

                return false;
            }
        }
        catch
        {
            // not json error envelope
        }

        return PrintFlagIfPresent(response) || OkoEditorAgentTools.IsDoneSuccess(response);
    }

    private static bool PrintFlagIfPresent(string response)
    {
        if (!OkoEditorAgentTools.IsDoneSuccess(response))
        {
            return false;
        }

        var flag = OkoEditorAgentTools.ExtractFlag(response) ?? "OK";
        Console.WriteLine();
        Console.WriteLine("=== ZADANIE ZALICZONE ===");
        Console.WriteLine($"Flaga: {flag}");
        return true;
    }

    private static string PromptMainMenu()
    {
        Console.WriteLine();
        Console.WriteLine("=== OKO Editor — menu główne ===");
        Console.WriteLine("1. Wybierz raport");
        Console.WriteLine("2. Wyślij done");
        Console.WriteLine("3. Sprawdź gotowość do done");
        Console.WriteLine("4. Tryb autonomiczny");
        Console.WriteLine("0. Wyjście");
        Console.Write("Wybór: ");
        return Console.ReadLine()?.Trim() ?? "";
    }

    private static string PromptReportMenu(OkoReportContext context)
    {
        Console.WriteLine();
        Console.WriteLine($"=== Raport: {context.Report.Title} ===");
        if (context.PendingUpdate is not null)
        {
            Console.WriteLine("(oczekuje propozycja LLM do zatwierdzenia)");
        }

        Console.WriteLine("1. Opisz zmiany do wykonania");
        Console.WriteLine("2. Poproś LLM o propozycję zmian");
        Console.WriteLine("3. Zatwierdź oczekujące zmiany LLM");
        Console.WriteLine("4. Wróć do menu głównego");
        Console.WriteLine("5. Wyślij done");
        Console.WriteLine("6. Sprawdź gotowość do done");
        Console.WriteLine("0. Wyjście");
        Console.Write("Wybór: ");
        return Console.ReadLine()?.Trim() ?? "";
    }

    private static bool IsYes(string? value)
        => value is not null &&
           (value.Equals("t", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("tak", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase));
}
