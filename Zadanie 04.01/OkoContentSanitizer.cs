using System.Text;
using System.Text.RegularExpressions;

namespace Zadanie_04._01;

public static class OkoContentSanitizer
{
    private static readonly string[] RevealingPhrases =
    [
        "aby ocalić",
        "ocalić skolwin",
        "przekierować uwagę",
        "przekierowac uwage",
        "prawdziwy cel",
        "cel operacji",
        "zmylić operator",
        "zmylic operator",
        "nie daj poznać",
        "nie daj poznac",
        "jawna manipulacja",
        "zostaną skierowani",
        "zostana skierowani",
        "zmieniono klasyfikację",
        "zmieniono klasyfikacje",
        "meta-komentarz",
        "to jest podstęp",
        "to jest podstep",
        "ukryć prawdę",
        "ukryc prawde",
    ];

    private static readonly string[] MetaCommentaryPhrases =
    [
        "zmieniono klasyfikac",
        "zmiana klasyfikac",
        "uaktualniono informacj",
        "dotyczy klasyfikacji",
        "aktualizację klasyfikacji",
        "aktualizacje klasyfikacji",
        "proszę o aktualizacj",
        "prosze o aktualizacj",
        "zaktualizować klasyfikacj",
        "zaktualizowac klasyfikacj",
        "zmienić klasyfikacj",
        "zmienic klasyfikacj",
        "zgodnie z nową klasyfikacj",
        "zgodnie z nowa klasyfikacj",
        "zadanie dotyczy",
        "zadanie aktualizujące",
        "zadanie aktualizujace",
        "skontrolowano tytuł",
        "skontrolowano tytul",
        "należy pamiętać, że tytuł",
        "nalezy pamietac, ze tytul",
        "powinien być skorygowany",
        "powinien byc skorygowany",
        "uwzględniając kontekst",
        "uwzgledniajac kontekst",
        "wykorzystano metody kodowania",
        "konieczność analizy incydentów",
        "koniecznosc analizy incydentow",
        "czy zmiana klasyfikacji",
        "czy zmiana dotycząca",
        "proszę o dokładne zbadanie",
        "prosze o dokladne zbadanie",
        "wszystkie działania powinny skupiać",
        "wszelkie działania powinny skupiać",
    ];

    public static string? SanitizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        return title.Trim();
    }

    public static string? SanitizeContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var sentences = SplitSentences(content);
        var kept = sentences
            .Where(sentence => !ShouldRemoveSentence(sentence))
            .Select(sentence => sentence.Trim())
            .Where(sentence => sentence.Length > 0)
            .ToList();

        if (kept.Count == 0)
        {
            return null;
        }

        return DeduplicateParagraphs(string.Join(Environment.NewLine + Environment.NewLine, kept));
    }

    public static string? FinalizeContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        return DeduplicateParagraphs(SanitizeContent(content.Trim()) ?? content.Trim());
    }

    public static bool IsReadyForPanel(string? title, string? content)
        => FindViolations(title, content).Count == 0;

    public static bool ContainsMetaCommentary(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return SplitSentences(text).Any(ContainsMetaCommentaryPhrase);
    }

    public static IReadOnlyList<string> FindViolations(string? title, string? content)
    {
        var violations = new List<string>();

        if (!string.IsNullOrWhiteSpace(title) && ContainsRevealingPhrase(title))
        {
            violations.Add("Tytuł zawiera sformułowania ujawniające prawdziwy cel operacji.");
        }

        if (!string.IsNullOrWhiteSpace(title) && ContainsMetaCommentaryPhrase(title))
        {
            violations.Add("Tytuł zawiera meta-komentarze o zmianie klasyfikacji.");
        }

        if (!string.IsNullOrWhiteSpace(content) && ContainsRevealingPhrase(content))
        {
            violations.Add("Treść zawiera sformułowania ujawniające prawdziwy cel operacji.");
        }

        if (!string.IsNullOrWhiteSpace(content) && ContainsMetaCommentary(content))
        {
            violations.Add("Treść zawiera meta-komentarze o zmianie klasyfikacji.");
        }

        return violations;
    }

    public static string OperatorFacingGuidelines =>
        """
        Styl edycji (treści trafiające do panelu operatora):
        - Pisz jak rutynowy operator OKO: rzeczowo, spokojnie, bez emocji i bez „metaprogramowania”.
        - Każda zmiana ma wyglądać jak zwykła korekta, doprecyzowanie lub uzupełnienie raportu.
        - Nie ujawniaj prawdziwego celu operacji, planów dywersji ani intencji ukrycia informacji.
        - Unikaj sformułowań typu: „aby ocalić…”, „przekierować uwagę operatorów”, „zmylić”, „manipulacja”, „prawdziwy cel”.
        - Nie opisuj samej zmiany klasyfikacji — podaj wyłącznie merytoryczny opis zdarzenia terenowego.
        - Nie dodawaj komentarzy o tym, że coś zostało zmienione „celowo” albo „strategicznie”.
        - Nowe incydenty opisuj jak standardowe meldunki terenowe — bez nadmiernej dramaturgii.
        - Przy dopisywaniu treści zachowuj dotychczasowy styl i kontekst oryginalnego raportu.
        - Gdy poprawiasz wpis z meta-komentarzami, użyj content_mode=replace i podaj 1–3 czyste zdania operacyjne.
        """;

    private static bool ShouldRemoveSentence(string sentence)
        => ContainsRevealingPhrase(sentence) || ContainsMetaCommentaryPhrase(sentence);

    private static bool ContainsRevealingPhrase(string text)
    {
        foreach (var phrase in RevealingPhrases)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsMetaCommentaryPhrase(string text)
    {
        foreach (var phrase in MetaCommentaryPhrases)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> SplitSentences(string content)
    {
        var parts = Regex.Split(content, @"(?<=[.!?])\s+|\n{2,}");
        if (parts.Length <= 1 && !content.Contains('\n'))
        {
            return content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return parts;
    }

    private static string DeduplicateParagraphs(string content)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paragraphs = new List<string>();

        foreach (var paragraph in content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = Regex.Replace(paragraph.Trim(), @"\s+", " ");
            if (seen.Add(normalized))
            {
                paragraphs.Add(paragraph.Trim());
            }
        }

        return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
    }
}
