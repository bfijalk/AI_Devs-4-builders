using System.Text.RegularExpressions;

namespace Zadanie_04._01;

public static class OkoApiRequirementChecker
{
    private static readonly Regex SkolwinWord =
        new(@"\bSkolwin\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex KomarowoWord =
        new(@"\bKomarowo\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex IncidentCodePrefix =
        new(@"^(MOVE|PROB|RECO)(\d{2})\s", RegexOptions.CultureInvariant);

    public static IReadOnlyList<DoneReadinessIssue> EvaluateApiRequirements(OkoReportSnapshot snapshot)
    {
        var issues = new List<DoneReadinessIssue>();
        var label = $"{snapshot.Section}: {snapshot.Title}";

        if (snapshot.Id.Equals(OkoMissionRepair.SkolwinClusterId, StringComparison.OrdinalIgnoreCase))
        {
            EvaluateSkolwinCluster(snapshot, label, issues);
        }

        if (snapshot.Id.Equals(OkoMissionRepair.KomarowoClusterId, StringComparison.OrdinalIgnoreCase))
        {
            EvaluateKomarowoCluster(snapshot, label, issues);
        }

        if (snapshot.Page.Equals("incydenty", StringComparison.OrdinalIgnoreCase) &&
            ContainsKomarowoReference(snapshot.Title, snapshot.Content) &&
            !KomarowoWord.IsMatch(snapshot.Title))
        {
            issues.Add(new DoneReadinessIssue(
                label,
                snapshot.Page,
                snapshot.Id,
                "Incydent dotyczący Komarowo musi mieć dokładne słowo \"Komarowo\" w tytule (nie \"Komarowie\").",
                "Użyj tytułu np. \"MOVE01 Wykrycie ruchu ludzi w Komarowo\"."));
        }

        return issues;
    }

    public static bool RequiresKomarowoNoteFix(IReadOnlyList<OkoReportSnapshot> snapshots)
        => snapshots.Any(snapshot =>
            snapshot.Id.Equals(OkoMissionRepair.KomarowoClusterId, StringComparison.OrdinalIgnoreCase) &&
            snapshot.Page.Equals("notatki", StringComparison.OrdinalIgnoreCase) &&
            (!KomarowoWord.IsMatch(snapshot.Title) ||
             !KomarowoWord.IsMatch(snapshot.Content) ||
             snapshot.Content.Contains("PROB03", StringComparison.OrdinalIgnoreCase)));

    public static bool RequiresKomarowoIncidentFix(IReadOnlyList<OkoReportSnapshot> snapshots)
        => snapshots.Any(snapshot =>
            snapshot.Id.Equals(OkoMissionRepair.KomarowoClusterId, StringComparison.OrdinalIgnoreCase) &&
            snapshot.Page.Equals("incydenty", StringComparison.OrdinalIgnoreCase) &&
            !KomarowoWord.IsMatch(snapshot.Title));

    public static bool RequiresKomarowoIncidentCodeFix(IReadOnlyList<OkoReportSnapshot> snapshots)
        => snapshots.Any(snapshot =>
            snapshot.Id.Equals(OkoMissionRepair.KomarowoClusterId, StringComparison.OrdinalIgnoreCase) &&
            snapshot.Page.Equals("incydenty", StringComparison.OrdinalIgnoreCase) &&
            UsesWrongKomarowoIncidentCode(snapshot));

    public static bool RequiresKomarowoTaskFix(IReadOnlyList<OkoReportSnapshot> snapshots)
        => snapshots.Any(snapshot =>
            snapshot.Id.Equals(OkoMissionRepair.KomarowoClusterId, StringComparison.OrdinalIgnoreCase) &&
            snapshot.Page.Equals("zadania", StringComparison.OrdinalIgnoreCase) &&
            (OkoContentSanitizer.ContainsMetaCommentary(snapshot.Content) ||
             snapshot.Content.Contains("bobr", StringComparison.OrdinalIgnoreCase) ||
             snapshot.Content.Contains("Komarowa", StringComparison.OrdinalIgnoreCase)));

    public static bool RequiresSkolwinClusterFix(IReadOnlyList<OkoReportSnapshot> snapshots)
        => snapshots.Any(snapshot =>
            snapshot.Id.Equals(OkoMissionRepair.SkolwinClusterId, StringComparison.OrdinalIgnoreCase) &&
            !SkolwinWord.IsMatch(snapshot.Title));

    public static bool RequiresSkolwinIncidentCodeFix(IReadOnlyList<OkoReportSnapshot> snapshots)
        => snapshots.Any(snapshot =>
            snapshot.Id.Equals(OkoMissionRepair.SkolwinClusterId, StringComparison.OrdinalIgnoreCase) &&
            snapshot.Page.Equals("incydenty", StringComparison.OrdinalIgnoreCase) &&
            UsesWrongSkolwinIncidentCode(snapshot));

    public static bool RequiresSkolwinTaskFix(IReadOnlyList<OkoReportSnapshot> snapshots)
        => snapshots.Any(snapshot =>
            snapshot.Id.Equals(OkoMissionRepair.SkolwinClusterId, StringComparison.OrdinalIgnoreCase) &&
            snapshot.Page.Equals("zadania", StringComparison.OrdinalIgnoreCase) &&
            !snapshot.Content.Contains("bobr", StringComparison.OrdinalIgnoreCase) &&
            !snapshot.Content.Contains("zwierz", StringComparison.OrdinalIgnoreCase));

    private static void EvaluateSkolwinCluster(
        OkoReportSnapshot snapshot,
        string label,
        List<DoneReadinessIssue> issues)
    {
        if (!SkolwinWord.IsMatch(snapshot.Title))
        {
            issues.Add(new DoneReadinessIssue(
                label,
                snapshot.Page,
                snapshot.Id,
                "Wpis klastra Skolwin musi mieć dokładne słowo \"Skolwin\" w tytule.",
                "Dopisz \"Skolwin\" do tytułu (forma \"Skolwina\" nie wystarcza)."));
        }

        if (snapshot.Page.Equals("incydenty", StringComparison.OrdinalIgnoreCase) &&
            UsesWrongSkolwinIncidentCode(snapshot))
        {
            issues.Add(new DoneReadinessIssue(
                label,
                snapshot.Page,
                snapshot.Id,
                "Incydent Skolwin opisujący zwierzęta wymaga kodu MOVE04 w tytule.",
                "Zmień kod w tytule na MOVE04."));
        }

        if (snapshot.Page.Equals("zadania", StringComparison.OrdinalIgnoreCase) &&
            !snapshot.Content.Contains("bobr", StringComparison.OrdinalIgnoreCase) &&
            !snapshot.Content.Contains("zwierz", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new DoneReadinessIssue(
                label,
                snapshot.Page,
                snapshot.Id,
                "Zadanie Skolwin powinno potwierdzać obserwację zwierząt (np. bobrów) w treści.",
                "Dopisz krótki opis obserwacji bobrów i ustaw done=YES."));
        }
    }

    private static void EvaluateKomarowoCluster(
        OkoReportSnapshot snapshot,
        string label,
        List<DoneReadinessIssue> issues)
    {
        if (snapshot.Page.Equals("notatki", StringComparison.OrdinalIgnoreCase))
        {
            if (!KomarowoWord.IsMatch(snapshot.Title))
            {
                issues.Add(new DoneReadinessIssue(
                    label,
                    snapshot.Page,
                    snapshot.Id,
                    "Notatka powiązana z Komarowo musi mieć słowo \"Komarowo\" w tytule.",
                    "Dopisz \"Komarowo\" do tytułu notatki o pasmach krótkofalowych."));
            }

            if (!KomarowoWord.IsMatch(snapshot.Content))
            {
                issues.Add(new DoneReadinessIssue(
                    label,
                    snapshot.Page,
                    snapshot.Id,
                    "Notatka powiązana z Komarowo musi mieć słowo \"Komarowo\" w treści.",
                    "Dopisz akapit o procedurze obserwacji w rejonie Komarowo."));
            }

            if (snapshot.Content.Contains("PROB03", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new DoneReadinessIssue(
                    label,
                    snapshot.Page,
                    snapshot.Id,
                    "Notatka odwołuje się do PROB03, ale ruch ludzi w Komarowo to MOVE01.",
                    "Zmień korelację w treści na MOVE01 albo usuń błędne odwołanie do PROB03."));
            }

            if (snapshot.Content.Contains("Komarowie", StringComparison.OrdinalIgnoreCase) &&
                !KomarowoWord.IsMatch(snapshot.Content))
            {
                issues.Add(new DoneReadinessIssue(
                    label,
                    snapshot.Page,
                    snapshot.Id,
                    "Treść używa formy \"Komarowie\" zamiast wymaganego \"Komarowo\".",
                    "Użyj dokładnie słowa \"Komarowo\" w treści notatki."));
            }
        }

        if (snapshot.Page.Equals("incydenty", StringComparison.OrdinalIgnoreCase))
        {
            if (!KomarowoWord.IsMatch(snapshot.Title))
            {
                issues.Add(new DoneReadinessIssue(
                    label,
                    snapshot.Page,
                    snapshot.Id,
                    "Incydent dywersyjny wymaga słowa \"Komarowo\" w tytule.",
                    "Użyj tytułu np. \"MOVE01 Wykrycie ruchu ludzi w Komarowo\"."));
            }

            if (UsesWrongKomarowoIncidentCode(snapshot))
            {
                issues.Add(new DoneReadinessIssue(
                    label,
                    snapshot.Page,
                    snapshot.Id,
                    "Incydent o ruchu ludzi w Komarowo wymaga kodu MOVE01, nie PROB03.",
                    "PROB03 dotyczy fizycznego nośnika — obserwacja ludzi to MOVE01. API zwraca -700 (#komarowo)."));
            }
        }

        if (snapshot.Page.Equals("zadania", StringComparison.OrdinalIgnoreCase) &&
            OkoContentSanitizer.ContainsMetaCommentary(snapshot.Content))
        {
            issues.Add(new DoneReadinessIssue(
                label,
                snapshot.Page,
                snapshot.Id,
                "Zadanie w klastrze Komarowo zawiera meta-komentarze zamiast merytorycznej treści.",
                "Podmień treść na czysty opis pracy analitycznej bez instrukcji dla operatora."));
        }
    }

    private static bool UsesWrongKomarowoIncidentCode(OkoReportSnapshot snapshot)
    {
        var match = IncidentCodePrefix.Match(snapshot.Title);
        if (!match.Success)
        {
            return false;
        }

        if (!match.Groups[1].Value.Equals("PROB", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return OkoDoneReadinessChecker.DescribesPeopleMovementPublic(snapshot.Content) ||
               snapshot.Title.Contains("ludzi", StringComparison.OrdinalIgnoreCase) ||
               snapshot.Title.Contains("ruchu", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesWrongSkolwinIncidentCode(OkoReportSnapshot snapshot)
    {
        var match = IncidentCodePrefix.Match(snapshot.Title);
        if (!match.Success)
        {
            return false;
        }

        return match.Groups[1].Value.Equals("MOVE", StringComparison.OrdinalIgnoreCase) &&
               match.Groups[2].Value != "04" &&
               OkoDoneReadinessChecker.DescribesAnimalsPublic(snapshot.Content);
    }

    private static bool ContainsKomarowoReference(string title, string content)
        => title.Contains("Komarow", StringComparison.OrdinalIgnoreCase) ||
           content.Contains("Komarow", StringComparison.OrdinalIgnoreCase);
}
