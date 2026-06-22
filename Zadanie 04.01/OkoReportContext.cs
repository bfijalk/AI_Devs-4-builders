namespace Zadanie_04._01;

public sealed record OkoPendingUpdate(
    string Page,
    string Id,
    string? Title,
    string? Content,
    string? Done,
    string Reason);

public sealed class OkoReportContext
{
    public required OkoWebEntry Entry { get; init; }
    public required OkoWebReport Report { get; set; }
    public required string Page { get; init; }
    public required string Id { get; init; }
    public IReadOnlyList<OkoWebEntry> RelatedEntries { get; init; } = [];
    public OkoPendingUpdate? PendingUpdate { get; set; }

    public static OkoReportContext From(
        OkoWebEntry entry,
        OkoWebReport report,
        IReadOnlyList<OkoWebEntry>? catalog = null)
    {
        var (page, id) = ParseReportReference(entry.Url);
        var related = catalog?
            .Where(item => ParseReportReference(item.Url).Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];

        return new OkoReportContext
        {
            Entry = entry,
            Report = report,
            Page = page,
            Id = id,
            RelatedEntries = related,
        };
    }

    public static (string Page, string Id) ParseReportReference(string url)
    {
        var path = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(url).AbsolutePath
            : url;

        var parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            throw new InvalidOperationException($"Nie udało się odczytać page/id z adresu: {url}");
        }

        return (parts[0], parts[1]);
    }

    public string Summary =>
        $"""
        Tytuł: {Report.Title}
        Sekcja: {Report.Section}
        Page (API): {Page}
        Id (API): {Id}
        URL: {Report.Url}

        Treść:
        {Report.Content}

        Powiązane wpisy (ten sam identyfikator):
        {FormatRelatedEntries()}
        """;

    private string FormatRelatedEntries()
    {
        if (RelatedEntries.Count == 0)
        {
            return "- brak danych w katalogu";
        }

        return string.Join(
            Environment.NewLine,
            RelatedEntries.Select(entry =>
            {
                var (page, _) = ParseReportReference(entry.Url);
                return $"- [{entry.Section}] page={page}, tytuł=\"{entry.Title}\"";
            }));
    }
}
