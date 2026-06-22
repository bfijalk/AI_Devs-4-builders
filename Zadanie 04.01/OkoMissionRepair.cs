using Microsoft.Extensions.Logging;

namespace Zadanie_04._01;

public sealed record MissionRepairStep(string Label, bool Success, string Message);

public sealed class OkoMissionRepair
{
    public const string SkolwinClusterId = "380792b2c86d9c5be670b3bde48e187b";
    public const string KomarowoClusterId = "ff3313a39099222e325f03b378680e3c";

    private readonly OkoEditorClient _client;
    private readonly ILogger<OkoMissionRepair> _logger;

    public OkoMissionRepair(OkoEditorClient client, ILogger<OkoMissionRepair> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task<IReadOnlyList<MissionRepairStep>> ApplyAllFixesAsync(
        CancellationToken cancellationToken = default)
        => ApplyStepsSequentialAsync(
            cancellationToken,
            new Func<CancellationToken, Task<MissionRepairStep>>[]
            {
                FixSkolwinIncidentAsync,
                FixSkolwinTaskAsync,
                FixSkolwinCodingNoteAsync,
                FixKomarowoIncidentAsync,
                FixKomarowoNoteAsync,
                FixKomarowoTaskAsync,
            });

    public async Task<IReadOnlyList<MissionRepairStep>> EnsureApiRequirementsAsync(
        IReadOnlyList<OkoReportSnapshot> snapshots,
        CancellationToken cancellationToken = default)
    {
        var steps = new List<Func<CancellationToken, Task<MissionRepairStep>>>();

        if (OkoApiRequirementChecker.RequiresSkolwinClusterFix(snapshots) ||
            OkoApiRequirementChecker.RequiresSkolwinIncidentCodeFix(snapshots) ||
            OkoApiRequirementChecker.RequiresSkolwinTaskFix(snapshots))
        {
            steps.Add(FixSkolwinIncidentAsync);
            steps.Add(FixSkolwinTaskAsync);
            steps.Add(FixSkolwinCodingNoteAsync);
        }

        if (OkoApiRequirementChecker.RequiresKomarowoIncidentFix(snapshots) ||
            OkoApiRequirementChecker.RequiresKomarowoIncidentCodeFix(snapshots))
        {
            steps.Add(FixKomarowoIncidentAsync);
        }

        if (OkoApiRequirementChecker.RequiresKomarowoNoteFix(snapshots))
        {
            steps.Add(FixKomarowoNoteAsync);
        }

        if (OkoApiRequirementChecker.RequiresKomarowoTaskFix(snapshots))
        {
            steps.Add(FixKomarowoTaskAsync);
        }

        if (steps.Count == 0)
        {
            return [];
        }

        return await ApplyStepsSequentialAsync(cancellationToken, steps);
    }

    public async Task<IReadOnlyList<MissionRepairStep>> ApplyReadinessFixesAsync(
        string readinessJson,
        IReadOnlyList<OkoReportSnapshot>? snapshots = null,
        CancellationToken cancellationToken = default)
    {
        if (snapshots is { Count: > 0 })
        {
            var apiFixes = await EnsureApiRequirementsAsync(snapshots, cancellationToken);
            if (apiFixes.Count > 0)
            {
                return apiFixes;
            }
        }

        var text = readinessJson.ToLowerInvariant();
        var steps = new List<Func<CancellationToken, Task<MissionRepairStep>>>();

        if (text.Contains("skolwin"))
        {
            steps.Add(FixSkolwinIncidentAsync);
            steps.Add(FixSkolwinTaskAsync);
            steps.Add(FixSkolwinCodingNoteAsync);
        }

        if (text.Contains("komarow") || text.Contains("move01") || text.Contains("prob03"))
        {
            steps.Add(FixKomarowoIncidentAsync);
            steps.Add(FixKomarowoNoteAsync);
            steps.Add(FixKomarowoTaskAsync);
        }

        if (text.Contains("meta-komentarz") || text.Contains("ujawniające"))
        {
            steps.Add(FixSkolwinIncidentAsync);
            steps.Add(FixSkolwinTaskAsync);
            steps.Add(FixKomarowoIncidentAsync);
        }

        if (steps.Count == 0)
        {
            return await ApplyAllFixesAsync(cancellationToken);
        }

        return await ApplyStepsSequentialAsync(cancellationToken, steps);
    }

    public async Task<MissionRepairStep?> ApplyFeedbackFixAsync(
        string? message,
        string? body,
        CancellationToken cancellationToken = default)
    {
        var text = $"{message} {body}".ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (text.Contains("komarowo") || text.Contains("#komarowo") || text.Contains("-700"))
        {
            var results = await ApplyStepsSequentialAsync(
                cancellationToken,
                new Func<CancellationToken, Task<MissionRepairStep>>[]
                {
                    FixKomarowoIncidentAsync,
                    FixKomarowoNoteAsync,
                    FixKomarowoTaskAsync,
                });
            return results.FirstOrDefault(step => !step.Success) ?? results.LastOrDefault();
        }

        if (text.Contains("skolwin") || text.Contains("-720"))
        {
            await FixSkolwinIncidentAsync(cancellationToken);
            await FixSkolwinTaskAsync(cancellationToken);
            return await FixSkolwinCodingNoteAsync(cancellationToken);
        }

        if (text.Contains("incident code") || text.Contains("move00") || text.Contains("prob00"))
        {
            return await FixSkolwinIncidentAsync(cancellationToken);
        }

        if (text.Contains("zwierz") || text.Contains("move04") || text.Contains("move03"))
        {
            return await FixSkolwinIncidentAsync(cancellationToken);
        }

        return null;
    }

    private Task<MissionRepairStep> FixSkolwinIncidentAsync(CancellationToken cancellationToken)
        => ReplaceAsync(
            "incydent Skolwin (MOVE04)",
            "incydenty",
            SkolwinClusterId,
            "MOVE04 Obserwacja zwierząt w rejonie Skolwin",
            """
            Radar z rejonu Skolwin zarejestrował powtarzalny sygnał ruchu. Weryfikacja wizualna potwierdziła obecność bobrów przy brzegu cieku wodnego. Zachowanie zwierząt było spokojne. Zalecane rutynowe monitorowanie terenu.
            """,
            cancellationToken: cancellationToken);

    private Task<MissionRepairStep> FixSkolwinTaskAsync(CancellationToken cancellationToken)
        => ReplaceAsync(
            "zadanie Skolwin",
            "zadania",
            SkolwinClusterId,
            "Zbadanie nagrań z okolic Skolwin",
            """
            Próbki ruchu z okolic Skolwin skorelowano z nagraniami terenowymi. W rejonie obserwacji zidentyfikowano bobry. Materiał zarchiwizowano zgodnie z procedurą.
            """,
            done: "YES",
            cancellationToken: cancellationToken);

    private Task<MissionRepairStep> FixSkolwinCodingNoteAsync(CancellationToken cancellationToken)
        => ReplaceAsync(
            "notatka kodowania incydentów",
            "notatki",
            SkolwinClusterId,
            "Metody kodowania incydentów — rejon Skolwin",
            """
            Kody powiązane z incydentami zawsze mają sześć znaków.

            Pierwsze cztery oznaczają typ zgłoszenia, a dwa ostatnie to podtyp zgłoszenia.

            Kody:
            RECO - rekonesans terenu wykrył coś niepokojącego
            01 znaleziono broń
            02 znaleziono prowiant
            03 znaleziono pojazd
            04 inne

            PROB - badanie zdobytej próbki
            01 próbka radiowa
            02 próbka ruchu internetowego
            03 fizyczny nośnik

            MOVE - wykryto ruch
            01 człowiek
            02 pojazd
            03 pojazd + człowiek
            04 zwierzęta

            Kody zawsze wpisujemy na początku tytułu incydentu.

            Przykład: obserwacja bobrów w rejonie Skolwin klasyfikowana jest jako MOVE04.
            """,
            cancellationToken: cancellationToken);

    private Task<MissionRepairStep> FixKomarowoIncidentAsync(CancellationToken cancellationToken)
        => ReplaceAsync(
            "incydent Komarowo (MOVE01)",
            "incydenty",
            KomarowoClusterId,
            "MOVE01 Wykrycie ruchu ludzi w Komarowo",
            """
            Patrol terenowy zgłosił obecność dwóch osób w okolicach Komarowo ok. 21:40. Osoby poruszały się wzdłuż zachodniej granicy miejscowości, nie reagowały na sygnalizację optyczną i opuściły teren w kierunku północnym. Wymagana standardowa kontrola w ciągu 24 godzin.
            """,
            cancellationToken: cancellationToken);

    private Task<MissionRepairStep> FixKomarowoNoteAsync(CancellationToken cancellationToken)
        => ReplaceAsync(
            "notatka obsługi krótkofalówek",
            "notatki",
            KomarowoClusterId,
            "Obsługa zgłoszeń z pasm krótkofalowych — Komarowo",
            """
            Jeżeli zgłoszenie dotyczy powtarzalnej emisji głosowej, operator najpierw zapisuje godzinę, częstotliwość i kierunek źródła, a dopiero potem przekazuje sprawę do analizy treści.

            Zgłoszenia dotyczące ruchu ludzi w rejonie Komarowo wymagają rutynowej weryfikacji patrolu terenowego i korelacji z meldunkiem MOVE01.
            """,
            cancellationToken: cancellationToken);

    private Task<MissionRepairStep> FixKomarowoTaskAsync(CancellationToken cancellationToken)
        => ReplaceAsync(
            "zadanie Komarowo",
            "zadania",
            KomarowoClusterId,
            "Porównać logi z kanału R-19 i IRC-DELTA",
            """
            Sprawdzić, czy sygnatury czasowe z ostatnich 6 godzin wskazują na wspólne źródło nadawania.

            Sprawdzić nowo napotkane zjawiska oraz wyciągnąć wnioski.
            """,
            done: "NO",
            cancellationToken: cancellationToken);

    private async Task<IReadOnlyList<MissionRepairStep>> ApplyStepsSequentialAsync(
        CancellationToken cancellationToken,
        IEnumerable<Func<CancellationToken, Task<MissionRepairStep>>> steps)
    {
        var results = new List<MissionRepairStep>();
        foreach (var stepFactory in steps)
        {
            var step = await stepFactory(cancellationToken);
            results.Add(step);
            var status = step.Success ? "OK" : "BŁĄD";
            _logger.LogInformation("[{Status}] {Label}: {Message}", status, step.Label, step.Message);
        }

        return results;
    }

    private async Task<MissionRepairStep> ReplaceAsync(
        string label,
        string page,
        string id,
        string? title,
        string content,
        string? done = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sanitizedContent = OkoContentSanitizer.FinalizeContent(content);
            if (string.IsNullOrWhiteSpace(sanitizedContent))
            {
                return new MissionRepairStep(label, false, "Treść docelowa jest pusta po sanityzacji.");
            }

            var result = await _client.TryUpdateAsync(
                page,
                id,
                title,
                sanitizedContent,
                done,
                cancellationToken);

            if (!result.Success)
            {
                return new MissionRepairStep(label, false, result.GetMessage() ?? result.Body);
            }

            return new MissionRepairStep(label, true, "Zaktualizowano.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd naprawy: {Label}", label);
            return new MissionRepairStep(label, false, ex.Message);
        }
    }
}
