using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class ThermalHealthRule : IHealthRule
{
    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        ThermalMetrics? metrics = sample.Thermal?.Metrics;
        double?         limit   = sample.Cpu?.Metrics.PerformanceLimitPercent;

        if (metrics == null && limit == null) {
            return new HealthReading { Resource = Resource };
        }

        List<HealthCondition> conditions = [];

        foreach (ThermalSensor sensor in metrics?.Sensors ?? []) {
            if (sensor.CriticalCelsius is { } critical) {
                conditions.Add(HealthCondition.Falling(
                    critical - sensor.Celsius,
                    thresholds.SensorWarningMarginCelsius,
                    thresholds.SensorCriticalMarginCelsius,
                    $"{SensorText(sensor)} near its {critical:0} °C limit"));
            }
        }

        if (metrics?.Pressure is { } pressure) {
            conditions.Add(PressureCondition(pressure, thresholds));
        }

        if (limit is { } percent) {
            conditions.Add(HealthCondition.Falling(
                percent,
                thresholds.PerformanceLimitWarningPercent,
                double.NegativeInfinity,
                $"CPU limited to {percent:0}%"));
        }

        HealthCondition worst   = HealthCondition.Worst(conditions);
        ThermalSensor?  hottest = metrics?.Sensors.MaxBy(sensor => sensor.Celsius);

        return new HealthReading {
            Resource    = Resource,
            Level       = worst.Level,
            Reason      = worst.Reason,
            Severity    = worst.Severity,
            Utilisation = hottest != null ? SensorText(hottest) : string.Empty,
            Saturation  = SaturationText(metrics?.Pressure, limit)
        };
    }

    private static HealthCondition PressureCondition(ThermalPressure pressure, HealthThresholds thresholds)
    {
        if (pressure < thresholds.ThermalPressureWarning) {
            return HealthCondition.Clear;
        }

        HealthLevel level = pressure >= thresholds.ThermalPressureCritical
            ? HealthLevel.Critical
            : HealthLevel.Warning;

        return new HealthCondition(level, $"thermal pressure {pressure}", 1.0 + (int)pressure);
    }

    public HealthResource Resource => HealthResource.Thermal;

    private static string SaturationText(ThermalPressure? pressure, double? limit)
    {
        if (pressure is { } level) {
            return $"Pressure {level}";
        }

        return limit is { } percent ? $"CPU limit {percent:0}%" : string.Empty;
    }

    private static string SensorText(ThermalSensor sensor)
    {
        string name = sensor.Component is ThermalComponent.Cpu or ThermalComponent.Gpu
            ? sensor.Component.ToString().ToUpperInvariant()
            : sensor.Component.ToString();

        return $"{name} {sensor.Celsius:0} °C";
    }
}
