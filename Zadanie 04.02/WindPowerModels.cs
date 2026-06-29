using System.Text.Json.Serialization;

namespace Zadanie_04._02;

public sealed record WeatherForecastEntry(
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("windMs")] double WindMs);

public sealed record WindPowerConfigSlot(
    string Timestamp,
    double WindMs,
    int PitchAngle,
    string TurbineMode,
    string? UnlockCode = null);

public sealed record WindPowerPlan(
    IReadOnlyList<WindPowerConfigSlot> StormProtection,
    WindPowerConfigSlot? Production,
    double TargetPowerKw);

public sealed record WindPowerAgentResult(
    bool Success,
    string? Flag,
    WindPowerPlan Plan,
    string? Error = null);
