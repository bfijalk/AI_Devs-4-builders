using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zadanie_04._01;

public sealed record DoneCompletionResult(
    bool Success,
    string? Flag,
    string? LastResponse,
    int Attempts);

public sealed class OkoDoneCompletionLoop
{
    private const int MaxAttempts = 10;

    private readonly OkoEditorAgentTools _tools;
    private readonly OkoMissionRepair _repair;
    private readonly ILogger<OkoDoneCompletionLoop> _logger;
    private string? _lastDoneErrorFingerprint;
    private int _sameDoneErrorStreak;

    public OkoDoneCompletionLoop(
        OkoEditorAgentTools tools,
        OkoMissionRepair repair,
        ILogger<OkoDoneCompletionLoop> logger)
    {
        _tools = tools;
        _repair = repair;
        _logger = logger;
    }

    public async Task<DoneCompletionResult> RunUntilDoneAsync(
        bool applyInitialRepair = true,
        CancellationToken cancellationToken = default)
    {
        if (applyInitialRepair)
        {
            Console.WriteLine("=== Wstępna naprawa wpisów ===");
            await LogRepairSteps(await _repair.ApplyAllFixesAsync(cancellationToken));
        }

        string? lastResponse = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.WriteLine();
            Console.WriteLine($"=== Próba zakończenia {attempt}/{MaxAttempts} ===");

            lastResponse = await _tools.CheckDoneReadinessAsync(cancellationToken);
            Console.WriteLine(lastResponse);

            if (!TryParseReady(lastResponse))
            {
                _logger.LogWarning("Weryfikacja gotowości nie przeszła — stosuję korekty.");
                var snapshots = await _tools.LoadCatalogSnapshotsAsync(cancellationToken);
                await LogRepairSteps(await _repair.ApplyReadinessFixesAsync(
                    lastResponse,
                    snapshots,
                    cancellationToken));
                continue;
            }

            var preDoneSnapshots = await _tools.LoadCatalogSnapshotsAsync(cancellationToken);
            var apiPrep = await _repair.EnsureApiRequirementsAsync(preDoneSnapshots, cancellationToken);
            if (apiPrep.Count > 0)
            {
                _logger.LogInformation("Wymuszono poprawki wymagań API przed done.");
                await LogRepairSteps(apiPrep);
                continue;
            }

            Console.WriteLine("Gotowość OK — wysyłam done...");
            lastResponse = await _tools.SendDoneAsync(cancellationToken);
            Console.WriteLine(lastResponse);

            if (OkoEditorAgentTools.IsDoneSuccess(lastResponse))
            {
                var flag = OkoEditorAgentTools.ExtractFlag(lastResponse) ?? "OK";
                Console.WriteLine();
                Console.WriteLine("=== ZADANIE ZALICZONE ===");
                Console.WriteLine($"Flaga: {flag}");
                return new DoneCompletionResult(true, flag, lastResponse, attempt);
            }

            if (!IsRetriableDoneFailure(lastResponse))
            {
                _logger.LogError("Done zwróciło błąd, którego nie da się naprawić automatycznie.");
                break;
            }

            var message = TryParseMessage(lastResponse);
            var body = TryParseBody(lastResponse);
            var errorCode = TryParseErrorCode(lastResponse, body);
            _logger.LogWarning("API odrzuciło done ({Code}): {Message}", errorCode, message);

            TrackDoneError(message, errorCode);

            MissionRepairStep? fix;
            if (_sameDoneErrorStreak >= 3)
            {
                _logger.LogWarning("Powtarzający się błąd done — uruchamiam pełną naprawę wpisów.");
                Console.WriteLine("Powtarzający się błąd done — uruchamiam pełną naprawę wpisów.");
                await LogRepairSteps(await _repair.ApplyAllFixesAsync(cancellationToken));
                _sameDoneErrorStreak = 0;
                _lastDoneErrorFingerprint = null;
                continue;
            }

            fix = await _repair.ApplyFeedbackFixAsync(message, body, cancellationToken);
            if (fix is null)
            {
                Console.WriteLine("Brak reguły dla tego błędu — uruchamiam pełną naprawę wpisów.");
                await LogRepairSteps(await _repair.ApplyAllFixesAsync(cancellationToken));
            }
            else
            {
                Console.WriteLine(fix.Success
                    ? $"Korekta na podstawie feedbacku API: {fix.Label}"
                    : $"Korekta nie powiodła się ({fix.Label}): {fix.Message}");
            }
        }

        return new DoneCompletionResult(false, null, lastResponse, MaxAttempts);
    }

    private async Task LogRepairSteps(IReadOnlyList<MissionRepairStep> steps)
    {
        foreach (var step in steps)
        {
            Console.WriteLine(step.Success
                ? $"  OK  {step.Label}"
                : $"  ERR {step.Label}: {step.Message}");
            await Task.CompletedTask;
        }
    }

    private static bool TryParseReady(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("ready", out var ready) &&
                   ready.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRetriableDoneFailure(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ok", out var ok) &&
                ok.ValueKind == JsonValueKind.True)
            {
                return false;
            }

            if (doc.RootElement.TryGetProperty("statusCode", out var status) &&
                status.GetInt32() is >= 400 and < 500)
            {
                return true;
            }

            if (doc.RootElement.TryGetProperty("message", out var message))
            {
                var text = message.GetString() ?? "";
                return text.Contains("Skolwin", StringComparison.OrdinalIgnoreCase) ||
                       text.Contains("Komarowo", StringComparison.OrdinalIgnoreCase) ||
                       text.Contains("incident", StringComparison.OrdinalIgnoreCase) ||
                       text.Contains("note", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }

    private static string? TryParseMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private void TrackDoneError(string? message, int? errorCode)
    {
        var fingerprint = $"{errorCode}:{message}".ToLowerInvariant();
        if (fingerprint == _lastDoneErrorFingerprint)
        {
            _sameDoneErrorStreak++;
        }
        else
        {
            _lastDoneErrorFingerprint = fingerprint;
            _sameDoneErrorStreak = 1;
        }
    }

    private static int? TryParseErrorCode(string json, string? body)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("body", out var bodyElement))
            {
                var bodyText = bodyElement.GetString();
                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    using var bodyDoc = JsonDocument.Parse(bodyText);
                    if (bodyDoc.RootElement.TryGetProperty("code", out var nestedCode) &&
                        nestedCode.TryGetInt32(out var nestedValue))
                    {
                        return nestedValue;
                    }
                }
            }
        }
        catch
        {
            // ignored
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var bodyDoc = JsonDocument.Parse(body);
            if (bodyDoc.RootElement.TryGetProperty("code", out var code) &&
                code.TryGetInt32(out var value))
            {
                return value;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static string? TryParseBody(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("body", out var body))
            {
                return body.GetString();
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }
}
