namespace Zadanie_04._01;

public sealed record OkoWebEntry(
    string Section,
    string Title,
    string Url,
    string? Preview);

public sealed record OkoWebReport(
    string Section,
    string Title,
    string Url,
    string Content,
    IReadOnlyList<string> Metadata,
    string HtmlPath,
    string MarkdownPath);
