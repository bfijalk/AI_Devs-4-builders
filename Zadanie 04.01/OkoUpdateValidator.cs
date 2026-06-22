using System.Text.RegularExpressions;

namespace Zadanie_04._01;

public static class OkoUpdateValidator
{
    private static readonly Regex IncidentTitlePrefix =
        new(@"^(MOVE|PROB|RECO)\d{2}\s", RegexOptions.CultureInvariant);

    public static string? ValidateTitle(string page, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        if (page.Equals("incydenty", StringComparison.OrdinalIgnoreCase) &&
            !IncidentTitlePrefix.IsMatch(title))
        {
            return "Tytuł incydentu musi zaczynać się od kodu MOVE00, PROB00 lub RECO00, np. \"MOVE03 Opis zdarzenia\".";
        }

        return null;
    }

    public static string? ValidateDoneField(string page, string? done)
    {
        if (string.IsNullOrWhiteSpace(done))
        {
            return null;
        }

        if (!page.Equals("zadania", StringComparison.OrdinalIgnoreCase))
        {
            return "Pole done można ustawiać tylko dla page=zadania.";
        }

        if (!done.Equals("YES", StringComparison.OrdinalIgnoreCase) &&
            !done.Equals("NO", StringComparison.OrdinalIgnoreCase))
        {
            return "Pole done musi mieć wartość YES lub NO.";
        }

        return null;
    }

    public static string MergeContent(string? existingContent, string newContent, string contentMode)
    {
        var normalizedNew = newContent.Trim();
        if (string.IsNullOrWhiteSpace(normalizedNew))
        {
            return existingContent?.Trim() ?? "";
        }

        if (contentMode.Equals("replace", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedNew;
        }

        var existing = existingContent?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(existing))
        {
            return normalizedNew;
        }

        if (existing.Contains(normalizedNew, StringComparison.Ordinal))
        {
            return existing;
        }

        return $"{existing}{Environment.NewLine}{Environment.NewLine}{normalizedNew}";
    }
}
