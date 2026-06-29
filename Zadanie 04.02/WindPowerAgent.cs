using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Zadanie_04._02;

public sealed class WindPowerAgent
{
    private static readonly Regex FlagRegex = new(@"\{FLG:[^}]+\}", RegexOptions.IgnoreCase);
    private static readonly TimeSpan SessionLimit = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(40);

    private readonly WindPowerClient _client;
    private readonly ILogger<WindPowerAgent> _logger;

    public WindPowerAgent(WindPowerClient client, ILogger<WindPowerAgent> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<WindPowerAgentResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var state = new RunState();

        try
        {
            await SendAsync(new { action = "start" }, cancellationToken);
            _logger.LogInformation("Sesja rozpoczęta.");

            await Task.WhenAll(
                SendAsync(new { action = "get", param = "weather" }, cancellationToken),
                SendAsync(new { action = "get", param = "powerplantcheck" }, cancellationToken));

            while (stopwatch.Elapsed < SessionLimit)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (state.DoneResponse is not null)
                {
                    break;
                }

                if (state.CanQueueUnlockCodes())
                {
                    state.UnlockCodesQueued += await QueueUnlockCodesAsync(state.PendingUnlockSlots, cancellationToken);
                    state.PendingUnlockSlots.Clear();
                    _logger.LogDebug("Zakolejkowano {Count} generatorów unlockCode.", state.UnlockCodesQueued);
                }

                if (state.CanSendConfig())
                {
                    state.ConfigResponse = await SendAsync(
                        new { action = "config", configs = BuildConfigPayload(state.Slots, state.UnlockCodes) },
                        cancellationToken);
                    _logger.LogInformation(
                        "Konfiguracja wysłana: {Message}",
                        state.ConfigResponse.Value.GetProperty("message").GetString());
                }

                if (state.CanQueueTurbineCheck())
                {
                    await SendAsync(new { action = "get", param = "turbinecheck" }, cancellationToken);
                    state.TurbineCheckQueued = true;
                }

                if (state.CanSendDone())
                {
                    state.DoneResponse = await SendAsync(new { action = "done" }, cancellationToken);
                    continue;
                }

                var response = await SendAsync(new { action = "getResult" }, cancellationToken);
                if (IsQueueNotReady(response))
                {
                    await Task.Delay(PollDelay, cancellationToken);
                    continue;
                }

                HandleQueueResponse(state, response);
            }

            stopwatch.Stop();
            _logger.LogInformation("Zakończono w {Elapsed:0.0}s", stopwatch.Elapsed.TotalSeconds);

            if (state.DoneResponse is null)
            {
                throw new TimeoutException(
                    $"Przekroczono limit czasu (unlock {state.UnlockCodes.Count}/{state.UnlockCodesQueued}).");
            }

            var flag = ExtractFlag(state.DoneResponse.Value);
            if (flag is null)
            {
                return new WindPowerAgentResult(
                    false,
                    null,
                    state.Plan ?? new WindPowerPlan([], null, 0),
                    state.DoneResponse.Value.GetRawText());
            }

            return new WindPowerAgentResult(true, flag, state.Plan!);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Agent zakończył się błędem po {Elapsed:0.0}s", stopwatch.Elapsed.TotalSeconds);
            return new WindPowerAgentResult(
                false,
                null,
                new WindPowerPlan([], null, 0),
                ex.Message);
        }
    }

    private void HandleQueueResponse(RunState state, JsonElement response)
    {
        if (response.TryGetProperty("unlockCode", out var unlockCodeProperty))
        {
            var unlockCode = unlockCodeProperty.GetString();
            if (string.IsNullOrWhiteSpace(unlockCode))
            {
                return;
            }

            var signed = response.GetProperty("signedParams");
            var timestamp = $"{signed.GetProperty("startDate").GetString()} {signed.GetProperty("startHour").GetString()}";
            state.UnlockCodes[timestamp] = unlockCode;
            _logger.LogDebug("Odebrano unlockCode dla {Timestamp}", timestamp);
            return;
        }

        if (!response.TryGetProperty("sourceFunction", out var sourceProperty))
        {
            return;
        }

        switch (sourceProperty.GetString())
        {
            case "weather" when !state.HasWeather:
                state.Forecast = ParseForecast(response);
                state.HasWeather = true;
                _logger.LogDebug("Pogoda odebrana.");
                state.TryBuildPlan(_logger);
                break;

            case "powerplantcheck" when !state.HasPowerplant:
                state.TargetKw = WindPowerPlanner.ParsePowerDeficitKw(
                    response.GetProperty("powerDeficitKw").GetString());
                state.HasPowerplant = true;
                _logger.LogDebug("Powerplantcheck odebrany. Cel: {Target:0.0} kW", state.TargetKw);
                state.TryBuildPlan(_logger);
                break;

            case "turbinecheck" when state.TurbineCheckQueued && !state.TurbineCheckDone:
                state.TurbineCheckDone = true;
                _logger.LogDebug(
                    "Turbinecheck: {Message}",
                    response.GetProperty("message").GetString());
                break;
        }
    }

    private async Task<int> QueueUnlockCodesAsync(
        IReadOnlyList<WindPowerConfigSlot> slots,
        CancellationToken cancellationToken)
    {
        if (slots.Count == 0)
        {
            return 0;
        }

        await Task.WhenAll(slots.Select(slot =>
        {
            var (date, hour) = SplitTimestamp(slot.Timestamp);
            return SendAsync(new
            {
                action = "unlockCodeGenerator",
                startDate = date,
                startHour = hour,
                windMs = slot.WindMs,
                pitchAngle = slot.PitchAngle,
            }, cancellationToken);
        }));

        return slots.Count;
    }

    private static Dictionary<string, object> BuildConfigPayload(
        IReadOnlyList<WindPowerConfigSlot> slots,
        IReadOnlyDictionary<string, string> codes)
    {
        var configs = new Dictionary<string, object>();

        foreach (var slot in slots)
        {
            if (!codes.TryGetValue(slot.Timestamp, out var unlockCode))
            {
                throw new InvalidOperationException($"Brak unlockCode dla {slot.Timestamp}");
            }

            configs[slot.Timestamp] = new
            {
                pitchAngle = slot.PitchAngle,
                turbineMode = slot.TurbineMode,
                unlockCode,
            };
        }

        return configs;
    }

    private async Task<JsonElement> SendAsync(object answer, CancellationToken cancellationToken)
    {
        var result = await _client.TrySendActionAsync(answer, cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Błąd API ({result.StatusCode}): {result.Body}");
        }

        return result.Document?.RootElement
               ?? throw new InvalidOperationException("Pusta odpowiedź API.");
    }

    private static bool IsQueueNotReady(JsonElement response)
    {
        if (response.TryGetProperty("code", out var code) && code.GetInt32() == 11)
        {
            return true;
        }

        var message = response.TryGetProperty("message", out var messageProperty)
            ? messageProperty.GetString()
            : null;

        return !string.IsNullOrWhiteSpace(message) &&
               message.Contains("No completed queued response", StringComparison.OrdinalIgnoreCase);
    }

    private static List<WeatherForecastEntry> ParseForecast(JsonElement weatherResponse)
    {
        if (!weatherResponse.TryGetProperty("forecast", out var forecast))
        {
            throw new InvalidOperationException("Brak prognozy pogody w odpowiedzi API.");
        }

        return JsonSerializer.Deserialize<List<WeatherForecastEntry>>(forecast.GetRawText())
               ?? throw new InvalidOperationException("Nie udało się odczytać prognozy pogody.");
    }

    private static (string Date, string Hour) SplitTimestamp(string timestamp)
    {
        var parts = timestamp.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"Niepoprawny timestamp: {timestamp}");
        }

        return (parts[0], parts[1]);
    }

    public static string? ExtractFlag(JsonElement response)
    {
        foreach (var property in response.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var match = FlagRegex.Match(property.Value.GetString() ?? string.Empty);
            if (match.Success)
            {
                return match.Value;
            }
        }

        if (response.TryGetProperty("message", out var message))
        {
            var match = FlagRegex.Match(message.GetString() ?? string.Empty);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }

    private sealed class RunState
    {
        public bool HasWeather { get; set; }
        public bool HasPowerplant { get; set; }
        public List<WeatherForecastEntry>? Forecast { get; set; }
        public double TargetKw { get; set; } = 4.5;
        public WindPowerPlan? Plan { get; private set; }
        public List<WindPowerConfigSlot> Slots { get; } = [];
        public List<WindPowerConfigSlot> PendingUnlockSlots { get; } = [];
        public Dictionary<string, string> UnlockCodes { get; } = new(StringComparer.Ordinal);
        public int UnlockCodesQueued { get; set; }
        public JsonElement? ConfigResponse { get; set; }
        public bool TurbineCheckQueued { get; set; }
        public bool TurbineCheckDone { get; set; }
        public JsonElement? DoneResponse { get; set; }

        public void TryBuildPlan(ILogger logger)
        {
            if (!HasWeather || !HasPowerplant || Forecast is null || Plan is not null)
            {
                return;
            }

            Plan = WindPowerPlanner.BuildPlan(Forecast, TargetKw);
            Slots.Clear();
            Slots.AddRange(Plan.StormProtection);
            if (Plan.Production is not null)
            {
                Slots.Add(Plan.Production);
            }

            PendingUnlockSlots.Clear();
            PendingUnlockSlots.AddRange(Slots);

            logger.LogInformation(
                "Plan: {StormCount} punktów ochrony, produkcja {Production}",
                Plan.StormProtection.Count,
                Plan.Production?.Timestamp ?? "brak");

            foreach (var slot in Plan.StormProtection)
            {
                logger.LogDebug(
                    "  {Timestamp}: wiatr {Wind:0.0} m/s -> pitch 90, idle",
                    slot.Timestamp,
                    slot.WindMs);
            }

            if (Plan.Production is null)
            {
                logger.LogWarning("Nie znaleziono punktu produkcji energii.");
                return;
            }

            var estimated = WindPowerPlanner.EstimatePowerKw(
                Plan.Production.WindMs,
                Plan.Production.PitchAngle);

            logger.LogInformation(
                "Punkt produkcji: {Timestamp}, wiatr {Wind:0.0} m/s, pitch {Pitch}, szac. {Power:0.0} kW (cel {Target:0.0} kW)",
                Plan.Production.Timestamp,
                Plan.Production.WindMs,
                Plan.Production.PitchAngle,
                estimated,
                Plan.TargetPowerKw);
        }

        public bool CanQueueUnlockCodes()
            => Plan is not null && PendingUnlockSlots.Count > 0;

        public bool CanSendConfig()
            => Plan is not null &&
               ConfigResponse is null &&
               UnlockCodesQueued > 0 &&
               UnlockCodes.Count == UnlockCodesQueued;

        public bool CanQueueTurbineCheck()
            => ConfigResponse is not null && !TurbineCheckQueued;

        public bool CanSendDone()
            => TurbineCheckDone && DoneResponse is null;
    }
}
