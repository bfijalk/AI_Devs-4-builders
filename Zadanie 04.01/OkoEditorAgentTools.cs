using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Zadanie_04._01;

public sealed class OkoEditorAgentTools
{
    private readonly OkoEditorClient _apiClient;
    private readonly OkoWebAgent _webAgent;
    private readonly ILogger<OkoEditorAgentTools> _logger;
    private IReadOnlyList<OkoWebEntry> _catalog = [];

    public OkoEditorAgentTools(
        OkoEditorClient apiClient,
        OkoWebAgent webAgent,
        ILogger<OkoEditorAgentTools> logger)
    {
        _apiClient = apiClient;
        _webAgent = webAgent;
        _logger = logger;
    }

    public void SetCatalog(IReadOnlyList<OkoWebEntry> catalog)
        => _catalog = catalog;

    public async Task<string> RefreshReportAsync(OkoReportContext context, CancellationToken cancellationToken = default)
    {
        var report = await _webAgent.FetchReportAsync(context.Entry, cancellationToken);
        context.Report = report;
        return JsonSerializer.Serialize(new
        {
            ok = true,
            title = report.Title,
            content = report.Content,
            markdownPath = report.MarkdownPath,
        });
    }

    public string ListRelatedEntries(OkoReportContext context)
    {
        var related = context.RelatedEntries
            .Select(entry => new
            {
                section = entry.Section,
                page = OkoReportContext.ParseReportReference(entry.Url).Page,
                id = context.Id,
                title = entry.Title,
                url = entry.Url,
            });

        return JsonSerializer.Serialize(new
        {
            ok = true,
            current = new { context.Page, context.Id, context.Report.Title },
            related,
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ApplyUpdateAsync(
        OkoReportContext context,
        string? page,
        string? id,
        string? title,
        string? content,
        string? done,
        string contentMode = "append",
        CancellationToken cancellationToken = default)
    {
        var targetPage = string.IsNullOrWhiteSpace(page) ? context.Page : page.Trim();
        var targetId = string.IsNullOrWhiteSpace(id) ? context.Id : id.Trim();

        var validationError =
            OkoUpdateValidator.ValidateTitle(targetPage, title) ??
            OkoUpdateValidator.ValidateDoneField(targetPage, done);

        if (validationError is not null)
        {
            return JsonSerializer.Serialize(new { ok = false, error = validationError });
        }

        title = OkoContentSanitizer.SanitizeTitle(title);
        string? resolvedContent = null;
        var contentWasSanitized = false;

        var isCurrentTarget = targetPage.Equals(context.Page, StringComparison.OrdinalIgnoreCase) &&
                              targetId.Equals(context.Id, StringComparison.OrdinalIgnoreCase);

        var sourceContent = isCurrentTarget
            ? context.Report.Content
            : await TryLoadContentAsync(targetPage, targetId, cancellationToken);

        var needsContentCleanup = OkoContentSanitizer.ContainsMetaCommentary(sourceContent) ||
                                  OkoContentSanitizer.FindViolations(null, sourceContent).Count > 0;

        if (!string.IsNullOrWhiteSpace(content) || needsContentCleanup)
        {
            var cleanedBase = contentMode.Equals("replace", StringComparison.OrdinalIgnoreCase)
                ? sourceContent?.Trim() ?? ""
                : OkoContentSanitizer.SanitizeContent(sourceContent) ?? "";

            if (needsContentCleanup && !contentMode.Equals("replace", StringComparison.OrdinalIgnoreCase))
            {
                contentMode = "replace";
                cleanedBase = OkoContentSanitizer.FinalizeContent(sourceContent) ?? cleanedBase;
            }

            resolvedContent = string.IsNullOrWhiteSpace(content)
                ? cleanedBase
                : contentMode.Equals("replace", StringComparison.OrdinalIgnoreCase)
                    ? OkoContentSanitizer.FinalizeContent(content)
                    : OkoUpdateValidator.MergeContent(cleanedBase, content, contentMode);

            var beforeFinalSanitize = resolvedContent;
            resolvedContent = OkoContentSanitizer.FinalizeContent(resolvedContent);
            contentWasSanitized = needsContentCleanup ||
                                  !string.Equals(beforeFinalSanitize, resolvedContent, StringComparison.Ordinal);
        }

        var stealthViolations = OkoContentSanitizer.FindViolations(title, resolvedContent);
        if (stealthViolations.Count > 0)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                error = "Treść lub tytuł nadaje się do panelu operatora — usuń sformułowania ujawniające intencję.",
                violations = stealthViolations,
                hint = OkoContentSanitizer.OperatorFacingGuidelines,
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        if (string.IsNullOrWhiteSpace(title) &&
            string.IsNullOrWhiteSpace(resolvedContent) &&
            string.IsNullOrWhiteSpace(done))
        {
            return JsonSerializer.Serialize(new { ok = false, error = "Po sanityzacji nie pozostała treść do wysłania." });
        }

        var result = await _apiClient.TryUpdateAsync(
            targetPage,
            targetId,
            title,
            resolvedContent,
            done,
            cancellationToken);

        if (!result.Success)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                statusCode = result.StatusCode,
                message = result.GetMessage(),
                body = result.Body,
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        if (targetPage.Equals(context.Page, StringComparison.OrdinalIgnoreCase) &&
            targetId.Equals(context.Id, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(resolvedContent))
        {
            context.Report = context.Report with { Content = resolvedContent };
        }

        var formatted = OkoEditorClient.FormatResult(result);
        if (!contentWasSanitized)
        {
            return formatted;
        }

        return JsonSerializer.Serialize(new
        {
            ok = true,
            contentSanitized = true,
            hint = "Usunięto meta-komentarze z treści przed wysłaniem do API. Sprawdź updated.content.",
            result = JsonSerializer.Deserialize<JsonElement>(formatted),
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<IReadOnlyList<OkoReportSnapshot>> LoadCatalogSnapshotsAsync(
        CancellationToken cancellationToken = default)
        => await LoadCatalogSnapshotsInternalAsync(cancellationToken);

    public async Task<string> CheckDoneReadinessAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = await LoadCatalogSnapshotsInternalAsync(cancellationToken);
        var report = OkoDoneReadinessChecker.Evaluate(snapshots);

        return JsonSerializer.Serialize(new
        {
            ok = report.Ready,
            ready = report.Ready,
            message = report.Ready
                ? OkoDoneReadinessChecker.BuildSummary(report)
                : "System nie jest gotowy do wysłania done.",
            summary = OkoDoneReadinessChecker.BuildSummary(report),
            issues = report.Issues.Select(issue => new
            {
                entry = issue.EntryLabel,
                page = issue.Page,
                id = issue.Id,
                problem = issue.Problem,
                suggestion = issue.Suggestion,
            }),
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> SendDoneAsync(CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.TryDoneAsync(cancellationToken);
        if (!result.Success)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                statusCode = result.StatusCode,
                message = result.GetMessage(),
                body = result.Body,
                hint = BuildDoneHint(result.GetMessage()),
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        return OkoEditorClient.FormatResult(result);
    }

    public string ProposeUpdate(
        OkoReportContext context,
        string? page,
        string? id,
        string? title,
        string? content,
        string? done,
        string reason,
        string contentMode = "append")
    {
        var targetPage = string.IsNullOrWhiteSpace(page) ? context.Page : page.Trim();
        var targetId = string.IsNullOrWhiteSpace(id) ? context.Id : id.Trim();

        var validationError =
            OkoUpdateValidator.ValidateTitle(targetPage, title) ??
            OkoUpdateValidator.ValidateDoneField(targetPage, done);

        if (validationError is not null)
        {
            return JsonSerializer.Serialize(new { ok = false, error = validationError });
        }

        title = OkoContentSanitizer.SanitizeTitle(title);

        if (string.IsNullOrWhiteSpace(title) &&
            string.IsNullOrWhiteSpace(content) &&
            string.IsNullOrWhiteSpace(done))
        {
            return JsonSerializer.Serialize(new { ok = false, error = "Brak pól do aktualizacji." });
        }

        var resolvedContent = string.IsNullOrWhiteSpace(content)
            ? null
            : OkoContentSanitizer.SanitizeContent(
                OkoUpdateValidator.MergeContent(
                    targetPage.Equals(context.Page, StringComparison.OrdinalIgnoreCase) &&
                    targetId.Equals(context.Id, StringComparison.OrdinalIgnoreCase)
                        ? context.Report.Content
                        : null,
                    content,
                    contentMode));

        var stealthViolations = OkoContentSanitizer.FindViolations(title, resolvedContent);
        if (stealthViolations.Count > 0)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                error = "Propozycja zawiera sformułowania ujawniające intencję operacji.",
                violations = stealthViolations,
            });
        }

        context.PendingUpdate = new OkoPendingUpdate(
            targetPage,
            targetId,
            title,
            resolvedContent,
            done,
            reason);

        return JsonSerializer.Serialize(new
        {
            ok = true,
            status = "pending",
            page = targetPage,
            id = targetId,
            title,
            content = resolvedContent,
            done,
            reason,
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ApprovePendingUpdateAsync(
        OkoReportContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.PendingUpdate is null)
        {
            return JsonSerializer.Serialize(new { ok = false, error = "Brak oczekujących zmian do zatwierdzenia." });
        }

        var pending = context.PendingUpdate;
        var result = await _apiClient.TryUpdateAsync(
            pending.Page,
            pending.Id,
            pending.Title,
            pending.Content,
            pending.Done,
            cancellationToken);

        if (!result.Success)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                statusCode = result.StatusCode,
                message = result.GetMessage(),
                body = result.Body,
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        context.PendingUpdate = null;
        return OkoEditorClient.FormatResult(result);
    }

    private async Task<string?> TryLoadContentAsync(
        string page,
        string id,
        CancellationToken cancellationToken)
    {
        var entry = _catalog.FirstOrDefault(item =>
            item.Url.Equals($"/{page}/{id}", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return null;
        }

        try
        {
            var report = await _webAgent.FetchReportAsync(entry, cancellationToken);
            return report.Content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się pobrać treści wpisu {Page}/{Id}", page, id);
            return null;
        }
    }

    private async Task<IReadOnlyList<OkoReportSnapshot>> LoadCatalogSnapshotsInternalAsync(
        CancellationToken cancellationToken)
    {
        var snapshots = new List<OkoReportSnapshot>();
        var entries = _catalog.Where(entry =>
        {
            var (page, _) = OkoReportContext.ParseReportReference(entry.Url);
            return page is "incydenty" or "notatki" or "zadania";
        });

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (page, id) = OkoReportContext.ParseReportReference(entry.Url);

            try
            {
                var report = await _webAgent.FetchReportAsync(entry, cancellationToken);
                snapshots.Add(new OkoReportSnapshot(
                    entry.Section,
                    page,
                    id,
                    report.Title,
                    report.Content));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pominięto wpis {Url} podczas weryfikacji done.", entry.Url);
            }
        }

        return snapshots;
    }

    private static string? BuildDoneHint(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Sprawdź, czy wszystkie wymagane edycje zostały wykonane poprawnie.";
        }

        if (message.Contains("Komarowo", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("#komarowo", StringComparison.OrdinalIgnoreCase))
        {
            return "Błąd #komarowo zwykle oznacza zły kod incydentu: ruch ludzi w Komarowo to MOVE01, nie PROB03. " +
                   "Notatka musi mieć \"Komarowo\" w tytule i treści oraz korelować z MOVE01, nie PROB03.";
        }

        if (message.Contains("Skolwin", StringComparison.OrdinalIgnoreCase))
        {
            return "Sprawdź wpisy powiązane ze Skolwinem: incydent powinien mieć poprawny kod (np. MOVE04 przy zwierzętach) " +
                   "oraz słowo \"Skolwin\" w tytule. Uruchom opcję \"Sprawdź gotowość do done\" przed ponowną próbą.";
        }

        if (message.Contains("incident code", StringComparison.OrdinalIgnoreCase))
        {
            return "Tytuły incydentów muszą zaczynać się od MOVE00, PROB00 lub RECO00.";
        }

        return null;
    }

    public static string? ExtractFlag(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("flag", out var flag))
            {
                return flag.GetString();
            }

            if (doc.RootElement.TryGetProperty("message", out var message))
            {
                return ExtractFlagFromText(message.GetString());
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    public static string? ExtractFlagFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var markerIdx = text.IndexOf("FLG:", StringComparison.Ordinal);
        if (markerIdx < 0)
        {
            return null;
        }

        var start = markerIdx;
        while (start > 0 && text[start - 1] == '{')
        {
            start--;
        }

        if (text[start] != '{')
        {
            return null;
        }

        if (start + 1 < text.Length && text[start + 1] == '{')
        {
            var end = text.IndexOf("}}", markerIdx, StringComparison.Ordinal);
            if (end >= 0)
            {
                return text[start..(end + 2)];
            }
        }

        var singleEnd = text.IndexOf('}', markerIdx);
        return singleEnd > start ? text[start..(singleEnd + 1)] : null;
    }

    public static bool IsDoneSuccess(string json)
    {
        if (!string.IsNullOrWhiteSpace(ExtractFlag(json)))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("code", out var code) &&
                code.ValueKind == JsonValueKind.Number &&
                code.GetInt32() == 0)
            {
                return true;
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }

    public static string FormatPendingUpdate(OkoPendingUpdate update)
    {
        var payload = new JsonObject
        {
            ["page"] = update.Page,
            ["id"] = update.Id,
            ["action"] = "update",
            ["reason"] = update.Reason,
        };

        if (!string.IsNullOrWhiteSpace(update.Title))
        {
            payload["title"] = update.Title;
        }

        if (!string.IsNullOrWhiteSpace(update.Content))
        {
            payload["content"] = update.Content;
        }

        if (!string.IsNullOrWhiteSpace(update.Done))
        {
            payload["done"] = update.Done;
        }

        return payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
