using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Zadanie_04._01;

public sealed class OkoWebAgent : IAsyncDisposable
{
    private const string BaseUrl = "https://oko.ag3nts.org";

    private static readonly JsonSerializerOptions EvaluateJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<OkoWebAgent> _logger;
    private readonly string _login;
    private readonly string _password;
    private readonly string _accessKey;
    private readonly string _outputDir;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public OkoWebAgent(
        ILogger<OkoWebAgent> logger,
        string login,
        string password,
        string accessKey,
        string outputDir = "files")
    {
        _logger = logger;
        _login = login;
        _password = password;
        _accessKey = accessKey;
        _outputDir = outputDir;
    }

    public async Task EnsureBrowserAsync(CancellationToken cancellationToken = default)
    {
        if (_page is not null)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await PlaywrightBootstrap.LaunchChromiumAsync(_playwright, _logger, cancellationToken);
        _page = await _browser.NewPageAsync();
        await _page.GotoAsync(BaseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
        });
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        await EnsureBrowserAsync(cancellationToken);
        var page = _page!;

        var loginForm = page.Locator("form").Filter(new LocatorFilterOptions
        {
            Has = page.Locator("[name='login']"),
        });

        if (await loginForm.CountAsync() == 0)
        {
            if (await IsAuthenticatedAsync())
            {
                _logger.LogInformation("Sesja już jest zalogowana.");
                return;
            }

            throw new InvalidOperationException("Nie znaleziono formularza logowania na stronie.");
        }

        _logger.LogInformation("Logowanie do panelu OKO...");

        await loginForm.Locator("[name='login']").FillAsync(_login);
        await loginForm.Locator("[name='password']").FillAsync(_password);
        await loginForm.Locator("[name='access_key']").FillAsync(_accessKey);
        await loginForm.Locator("button[type='submit'], input[type='submit']").First.ClickAsync();

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        if (await loginForm.CountAsync() > 0 && await loginForm.IsVisibleAsync())
        {
            var error = await page.Locator(".error-box").First.TextContentAsync();
            throw new InvalidOperationException(
                $"Logowanie nie powiodło się: {error?.Trim() ?? "nieznany błąd"}");
        }

        _logger.LogInformation("Zalogowano pomyślnie.");
    }

    public async Task<IReadOnlyList<OkoWebEntry>> DiscoverEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureBrowserAsync(cancellationToken);
        var page = _page!;

        var sectionPages = await DiscoverSectionPagesAsync(page);
        _logger.LogInformation("Znaleziono {Count} sekcji do przeszukania.", sectionPages.Count);

        var entries = new List<OkoWebEntry>();

        foreach (var section in sectionPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var absoluteUrl = ToAbsoluteUrl(section.Url);
            _logger.LogInformation("Skanuję sekcję: {Section} ({Url})", section.Title, absoluteUrl);

            await page.GotoAsync(absoluteUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
            });

            var sectionEntries = await ExtractEntriesFromCurrentPageAsync(page, section.Title);
            entries.AddRange(sectionEntries);
            _logger.LogInformation("  → {Count} wpisów", sectionEntries.Count);
        }

        var catalogPath = Path.Combine(_outputDir, "oko_catalog.json");
        Directory.CreateDirectory(_outputDir);
        await File.WriteAllTextAsync(
            catalogPath,
            JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        _logger.LogInformation("Katalog zapisany w: {Path}", catalogPath);
        return entries;
    }

    public OkoWebEntry? FindBestMatch(string query, IReadOnlyList<OkoWebEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var normalizedQuery = query.Trim();

        var exact = entries.FirstOrDefault(entry =>
            entry.Title.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
            entry.Url.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
            entry.Url.EndsWith(normalizedQuery.TrimStart('/'), StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return exact;
        }

        var containsMatches = entries
            .Where(entry =>
                entry.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                (entry.Preview?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                entry.Section.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (containsMatches.Count == 1)
        {
            return containsMatches[0];
        }

        if (containsMatches.Count > 1)
        {
            return containsMatches
                .OrderByDescending(entry => ScoreMatch(entry, normalizedQuery))
                .First();
        }

        return entries
            .OrderByDescending(entry => ScoreMatch(entry, normalizedQuery))
            .FirstOrDefault(entry => ScoreMatch(entry, normalizedQuery) > 0);
    }

    public async Task<OkoWebReport> FetchReportAsync(
        OkoWebEntry entry,
        CancellationToken cancellationToken = default)
    {
        await EnsureBrowserAsync(cancellationToken);
        var page = _page!;

        var absoluteUrl = ToAbsoluteUrl(entry.Url);
        _logger.LogInformation("Pobieram raport: {Title} ({Url})", entry.Title, absoluteUrl);

        await page.GotoAsync(absoluteUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
        });

        var snapshot = await EvaluateJsonAsync<PageSnapshot>(page, @"
            () => {
                const main = document.querySelector('main') ?? document.body;
                const titleNode =
                    main.querySelector('.hero-title') ??
                    main.querySelector('h1') ??
                    main.querySelector('h2');
                const contentNode =
                    main.querySelector('.detail-content') ??
                    main.querySelector('.detail-panel') ??
                    main.querySelector('article') ??
                    main;

                const metadata = [...main.querySelectorAll('.detail-meta, .detail-meta *')]
                    .map(node => node.textContent?.trim() ?? '')
                    .filter(Boolean);

                return {
                    title: titleNode?.textContent?.trim() ?? '',
                    content: contentNode?.textContent?.trim() ?? '',
                    metadata,
                    html: main.innerHTML ?? '',
                };
            }");

        var title = string.IsNullOrWhiteSpace(snapshot.Title) ? entry.Title : snapshot.Title;
        var safeName = SanitizeFileName(title);
        Directory.CreateDirectory(_outputDir);

        var htmlPath = Path.Combine(_outputDir, $"{safeName}.html");
        var markdownPath = Path.Combine(_outputDir, $"{safeName}.md");

        var htmlDocument = BuildHtmlDocument(title, snapshot.Html);
        var markdown = BuildMarkdownReport(entry.Section, title, absoluteUrl, snapshot);

        await File.WriteAllTextAsync(htmlPath, htmlDocument, cancellationToken);
        await File.WriteAllTextAsync(markdownPath, markdown, cancellationToken);

        _logger.LogInformation("Raport zapisany: {MarkdownPath}", markdownPath);

        return new OkoWebReport(
            entry.Section,
            title,
            absoluteUrl,
            snapshot.Content,
            snapshot.Metadata,
            htmlPath,
            markdownPath);
    }

    public async Task<OkoWebReport> SearchAndFetchReportAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var entries = await DiscoverEntriesAsync(cancellationToken);
        var match = FindBestMatch(query, entries)
            ?? throw new InvalidOperationException(
                $"Nie znaleziono raportu pasującego do zapytania: \"{query}\"");

        return await FetchReportAsync(match, cancellationToken);
    }

    private async Task<bool> IsAuthenticatedAsync()
    {
        var page = _page!;
        var logoutForm = page.Locator("form").Filter(new LocatorFilterOptions
        {
            Has = page.Locator("[name='action'][value='logout'], input[value='logout']"),
        });

        if (await logoutForm.CountAsync() > 0)
        {
            return true;
        }

        return await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Wyloguj" }).CountAsync() > 0;
    }

    private static async Task<IReadOnlyList<SectionPage>> DiscoverSectionPagesAsync(IPage page)
    {
        var currentPath = new Uri(page.Url).AbsolutePath;
        var pages = await EvaluateJsonAsync<SectionPage[]>(page, @"
            () => {
                const currentPath = window.location.pathname;
                const seen = new Set();
                const sections = [];

                const isSectionIndex = href => {
                    if (!href || !href.startsWith('/')) return false;
                    if (href.includes('/edit/') || href.includes('/delete/')) return false;
                    const parts = href.split('/').filter(Boolean);
                    return parts.length <= 1;
                };

                for (const link of document.querySelectorAll('a[href]')) {
                    const href = link.getAttribute('href') ?? '';
                    const text = (link.textContent ?? '').trim();
                    if (!isSectionIndex(href) || !text) continue;
                    if (seen.has(href)) continue;
                    seen.add(href);
                    sections.push({ title: text, url: href });
                }

                if (!seen.has(currentPath)) {
                    const heading =
                        document.querySelector('.hero-title')?.textContent?.trim() ||
                        document.querySelector('h1')?.textContent?.trim() ||
                        document.title.replace(/^OKO\\s*\\|\\s*/i, '').trim();
                    sections.unshift({ title: heading || currentPath, url: currentPath });
                }

                return sections;
            }");

        return pages
            .Where(pageInfo => !string.IsNullOrWhiteSpace(pageInfo.Title))
            .DistinctBy(pageInfo => pageInfo.Url)
            .ToList();
    }

    private static async Task<IReadOnlyList<OkoWebEntry>> ExtractEntriesFromCurrentPageAsync(
        IPage page,
        string sectionTitle)
    {
        var currentUrl = page.Url;
        var entries = await EvaluateJsonAsync<DiscoveredEntry[]>(page, @"
            () => {
                const results = [];
                const seen = new Set();

                const pushEntry = (href, title, preview) => {
                    if (!href || !href.startsWith('/') || seen.has(href)) return;
                    if (href.includes('/edit/') || href.includes('/delete/')) return;
                    const parts = href.split('/').filter(Boolean);
                    if (parts.length < 2) return;

                    const normalizedTitle = (title ?? '').trim();
                    const normalizedPreview = (preview ?? '').trim();
                    if (!normalizedTitle && !normalizedPreview) return;

                    seen.add(href);
                    results.push({
                        title: normalizedTitle || normalizedPreview,
                        url: href,
                        preview: normalizedPreview || null,
                    });
                };

                for (const link of document.querySelectorAll('a[href]')) {
                    const href = link.getAttribute('href') ?? '';
                    const article = link.querySelector('article');
                    const titleNode = link.querySelector('strong, h1, h2, h3, h4');
                    const previewNode = link.querySelector('p');

                    pushEntry(
                        href,
                        titleNode?.textContent ?? link.textContent,
                        previewNode?.textContent);
                }

                return results;
            }");

        return entries
            .Select(entry => new OkoWebEntry(
                sectionTitle,
                entry.Title,
                entry.Url,
                entry.Preview))
            .Where(entry => !string.Equals(entry.Url, new Uri(currentUrl).AbsolutePath, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(entry => entry.Url)
            .ToList();
    }

    private static int ScoreMatch(OkoWebEntry entry, string query)
    {
        var score = 0;
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var term in terms)
        {
            if (entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 3;
            }

            if (entry.Preview?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                score += 2;
            }

            if (entry.Section.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 1;
            }
        }

        return score;
    }

    private static async Task<T> EvaluateJsonAsync<T>(IPage page, string script)
    {
        var json = await page.EvaluateAsync<JsonElement>(script);
        return json.Deserialize<T>(EvaluateJsonOpts)
            ?? throw new InvalidOperationException("Nie udało się odczytać danych ze strony.");
    }

    private static string BuildMarkdownReport(
        string section,
        string title,
        string url,
        PageSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"- Sekcja: {section}");
        sb.AppendLine($"- URL: {url}");
        sb.AppendLine();

        if (snapshot.Metadata.Length > 0)
        {
            sb.AppendLine("## Metadane");
            sb.AppendLine();
            foreach (var item in snapshot.Metadata.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"- {item}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Treść");
        sb.AppendLine();
        sb.AppendLine(snapshot.Content);
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string BuildHtmlDocument(string title, string bodyHtml)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="pl">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>{System.Net.WebUtility.HtmlEncode(title)}</title>
            </head>
            <body>
            {bodyHtml}
            </body>
            </html>
            """;
    }

    private static string SanitizeFileName(string value)
    {
        var sanitized = Regex.Replace(value.Trim(), @"[^\w\-]+", "_");
        sanitized = Regex.Replace(sanitized, @"_+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "report" : sanitized[..Math.Min(sanitized.Length, 80)];
    }

    private static string ToAbsoluteUrl(string relativeOrAbsoluteUrl)
    {
        if (relativeOrAbsoluteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            relativeOrAbsoluteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return relativeOrAbsoluteUrl;
        }

        return $"{BaseUrl}{(relativeOrAbsoluteUrl.StartsWith('/') ? relativeOrAbsoluteUrl : $"/{relativeOrAbsoluteUrl}")}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null)
        {
            await _page.CloseAsync();
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }

    private sealed class SectionPage
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
    }

    private sealed class DiscoveredEntry
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string? Preview { get; set; }
    }

    private sealed class PageSnapshot
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string[] Metadata { get; set; } = [];
        public string Html { get; set; } = "";
    }
}
