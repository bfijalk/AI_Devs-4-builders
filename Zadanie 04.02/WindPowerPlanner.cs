namespace Zadanie_04._02;

public static class WindPowerPlanner
{
    private const double RatedPowerKw = 14;
    private const double DefaultCutoffWindMs = 14;
    private const double MinOperationalWindMs = 4;

    public static WindPowerPlan BuildPlan(
        IReadOnlyList<WeatherForecastEntry> forecast,
        double targetPowerKw,
        double cutoffWindMs = DefaultCutoffWindMs)
    {
        var stormProtection = forecast
            .Where(entry => entry.WindMs >= cutoffWindMs)
            .Select(entry => new WindPowerConfigSlot(
                entry.Timestamp,
                entry.WindMs,
                PitchAngle: 90,
                TurbineMode: "idle"))
            .ToList();

        var production = FindProductionSlot(forecast, targetPowerKw, cutoffWindMs);

        return new WindPowerPlan(stormProtection, production, targetPowerKw);
    }

    public static double ParsePowerDeficitKw(string? powerDeficit)
    {
        if (string.IsNullOrWhiteSpace(powerDeficit))
        {
            return 4.5;
        }

        var parts = powerDeficit
            .Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2 &&
            double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var low) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var high))
        {
            return (low + high) / 2;
        }

        if (double.TryParse(powerDeficit, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var single))
        {
            return single;
        }

        return 4.5;
    }

    public static double EstimatePowerKw(double windMs, int pitchAngle)
        => RatedPowerKw * WindYield(windMs) * PitchYield(pitchAngle);

    private static WindPowerConfigSlot? FindProductionSlot(
        IReadOnlyList<WeatherForecastEntry> forecast,
        double targetPowerKw,
        double cutoffWindMs)
    {
        WindPowerConfigSlot? best = null;
        var bestDistance = double.MaxValue;

        foreach (var entry in forecast)
        {
            if (entry.WindMs < MinOperationalWindMs || entry.WindMs >= cutoffWindMs)
            {
                continue;
            }

            foreach (var pitch in new[] { 0, 45 })
            {
                var estimated = EstimatePowerKw(entry.WindMs, pitch);
                if (estimated <= 0)
                {
                    continue;
                }

                var distance = Math.Abs(estimated - targetPowerKw);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new WindPowerConfigSlot(
                        entry.Timestamp,
                        entry.WindMs,
                        pitch,
                        "production");
                }
            }
        }

        if (best is not null)
        {
            return best;
        }

        var fallback = forecast
            .Where(entry => entry.WindMs >= MinOperationalWindMs && entry.WindMs < cutoffWindMs)
            .MaxBy(entry => entry.WindMs);

        return fallback is null
            ? null
            : new WindPowerConfigSlot(fallback.Timestamp, fallback.WindMs, 0, "production");
    }

    private static double WindYield(double windMs)
    {
        if (windMs >= 14)
        {
            return 0;
        }

        if (windMs >= 12)
        {
            return 1.0;
        }

        if (windMs >= 10)
        {
            return 0.95;
        }

        if (windMs >= 8)
        {
            return 0.65;
        }

        if (windMs >= 6)
        {
            return 0.35;
        }

        if (windMs >= 4)
        {
            return 0.125;
        }

        return 0;
    }

    private static double PitchYield(int pitchAngle) =>
        pitchAngle switch
        {
            0 => 1.0,
            45 => 0.65,
            90 => 0,
            _ => 0,
        };
}
