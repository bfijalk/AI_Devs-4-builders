using System.Text.Json;
using System.Text.RegularExpressions;

namespace Zadanie_04._01;

public sealed record DoneReadinessIssue(
    string EntryLabel,
    string Page,
    string Id,
    string Problem,
    string? Suggestion);

public sealed record DoneReadinessReport(
    bool Ready,
    IReadOnlyList<DoneReadinessIssue> Issues)
{
    public string ToJson() =>
        JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class OkoDoneReadinessChecker
{
    private static readonly Regex IncidentCodePrefix =
        new(@"^(MOVE|PROB|RECO)(\d{2})\s", RegexOptions.CultureInvariant);

    private static readonly Regex SkolwinWord =
        new(@"\bSkolwin\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly string[] AnimalIndicators =
    [
        "zwierz",
        "bobr",
        "bóbr",
        "bobry",
        "borsuk",
        "dzik",
        "sarna",
        "lis ",
        " lisa",
    ];

    public static DoneReadinessReport Evaluate(IReadOnlyList<OkoReportSnapshot> snapshots)
    {
        var issues = new List<DoneReadinessIssue>();

        foreach (var snapshot in snapshots)
        {
            EvaluateSnapshot(snapshot, issues);

            foreach (var apiIssue in OkoApiRequirementChecker.EvaluateApiRequirements(snapshot))
            {
                issues.Add(apiIssue);
            }
        }

        return new DoneReadinessReport(issues.Count == 0, issues);
    }

    public static string BuildSummary(DoneReadinessReport report)
    {
        if (report.Ready)
        {
            return "Wstępna weryfikacja: brak oczywistych problemów przed wysłaniem done.";
        }

        var lines = new List<string> { "Wstępna weryfikacja wykryła problemy:" };
        foreach (var issue in report.Issues)
        {
            lines.Add($"- [{issue.EntryLabel}] {issue.Problem}");
            if (!string.IsNullOrWhiteSpace(issue.Suggestion))
            {
                lines.Add($"  Sugestia: {issue.Suggestion}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void EvaluateSnapshot(OkoReportSnapshot snapshot, List<DoneReadinessIssue> issues)
    {
        var label = $"{snapshot.Section}: {snapshot.Title}";

        if (snapshot.Page.Equals("incydenty", StringComparison.OrdinalIgnoreCase))
        {
            EvaluateIncydent(snapshot, label, issues);
        }

        if (IsSkolwinRelated(snapshot))
        {
            if (!SkolwinWord.IsMatch(snapshot.Title))
            {
                issues.Add(new DoneReadinessIssue(
                    label,
                    snapshot.Page,
                    snapshot.Id,
                    "Wpis powiązany ze Skolwinem nie zawiera dokładnego słowa \"Skolwin\" w tytule.",
                    "Dopisz \"Skolwin\" do tytułu (np. zamiast wyłącznie formy \"Skolwina\")."));
            }
        }

        var stealthViolations = OkoContentSanitizer.FindViolations(snapshot.Title, snapshot.Content);
        foreach (var violation in stealthViolations)
        {
            issues.Add(new DoneReadinessIssue(
                label,
                snapshot.Page,
                snapshot.Id,
                violation,
                "Przepisz treść naturalnym językiem operatora, bez ujawniania intencji."));
        }

        if (snapshot.Content.Contains("niszczyciel", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new DoneReadinessIssue(
                label,
                snapshot.Page,
                snapshot.Id,
                "Treść zawiera agresywną rekomendację (niszczyciele).",
                "Usuń lub złagodź ten fragment — ma wyglądać jak spokojna obserwacja terenowa."));
        }
    }

    private static void EvaluateIncydent(
        OkoReportSnapshot snapshot,
        string label,
        List<DoneReadinessIssue> issues)
    {
        var match = IncidentCodePrefix.Match(snapshot.Title);
        if (!match.Success)
        {
            issues.Add(new DoneReadinessIssue(
                label,
                snapshot.Page,
                snapshot.Id,
                "Tytuł incydentu nie zaczyna się od kodu MOVE00, PROB00 lub RECO00.",
                "Użyj kodu z notatki o kodowaniu, np. \"MOVE04 ...\" albo \"PROB03 ...\"."));
            return;
        }

        var type = match.Groups[1].Value;
        var subtype = match.Groups[2].Value;

        if (type.Equals("MOVE", StringComparison.OrdinalIgnoreCase) &&
            DescribesAnimals(snapshot.Content) &&
            subtype != "04")
        {
            issues.Add(new DoneReadinessIssue(
                label,
                snapshot.Page,
                snapshot.Id,
                $"Incydent opisuje zwierzęta, ale kod to {type}{subtype} zamiast MOVE04.",
                "Zmień kod w tytule na MOVE04 i dopasuj opis do obserwacji zwierząt."));
        }
    }

    private static bool IsSkolwinRelated(OkoReportSnapshot snapshot)
    {
        if (snapshot.Id.Equals(OkoMissionRepair.SkolwinClusterId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (snapshot.Id.Equals(OkoMissionRepair.KomarowoClusterId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (snapshot.Page.Equals("incydenty", StringComparison.OrdinalIgnoreCase) &&
            snapshot.Title.Contains("Komarow", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return SkolwinWord.IsMatch(snapshot.Title) ||
               snapshot.Title.Contains("Skolwina", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DescribesAnimals(string content)
        => DescribesAnimalsPublic(content);

    public static bool DescribesAnimalsPublic(string content)
        => AnimalIndicators.Any(indicator =>
            content.Contains(indicator, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] PeopleMovementIndicators =
    [
        "ludzi",
        "osób",
        "osoby",
        "człowiek",
        "patrol terenowy",
        "dwóch osób",
        "dwoch osob",
    ];

    public static bool DescribesPeopleMovementPublic(string content)
        => PeopleMovementIndicators.Any(indicator =>
            content.Contains(indicator, StringComparison.OrdinalIgnoreCase));
}

public sealed record OkoReportSnapshot(
    string Section,
    string Page,
    string Id,
    string Title,
    string Content);
