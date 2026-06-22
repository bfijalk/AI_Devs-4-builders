using Microsoft.Extensions.Logging;

namespace Zadanie_04._01;

public sealed class OkoAutonomousAgent
{
    private readonly OkoWebAgent _webAgent;
    private readonly OkoEditorAgentTools _tools;
    private readonly OkoDoneCompletionLoop _completionLoop;
    private readonly ILogger<OkoAutonomousAgent> _logger;

    public OkoAutonomousAgent(
        OkoWebAgent webAgent,
        OkoEditorAgentTools tools,
        OkoDoneCompletionLoop completionLoop,
        ILogger<OkoAutonomousAgent> logger)
    {
        _webAgent = webAgent;
        _tools = tools;
        _completionLoop = completionLoop;
        _logger = logger;
    }

    public async Task<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        await _webAgent.LoginAsync(cancellationToken);
        var catalog = await _webAgent.DiscoverEntriesAsync(cancellationToken);
        _tools.SetCatalog(catalog);

        Console.WriteLine();
        Console.WriteLine("=== Tryb autonomiczny ===");
        Console.WriteLine($"Wpisów w katalogu: {catalog.Count}");

        var result = await _completionLoop.RunUntilDoneAsync(applyInitialRepair: true, cancellationToken);
        if (result.Success)
        {
            return true;
        }

        _logger.LogError("Nie udało się zakończyć zadania automatycznie po {Attempts} próbach.", result.Attempts);
        Console.WriteLine();
        Console.WriteLine("Nie udało się zakończyć zadania automatycznie. Sprawdź logi i uruchom ponownie.");
        return false;
    }
}
