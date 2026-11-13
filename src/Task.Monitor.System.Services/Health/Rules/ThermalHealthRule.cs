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
                    new HealthCause(HealthCauseKind.SensorNearLimit, sensor.Celsius, sensor.Component.ToString(), critical)));
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
                new HealthCause(HealthCauseKind.CpuPerformanceLimit, percent)));
        }

        HealthCondition worst   = HealthCondition.Worst(conditions);
        ThermalSensor?  hottest = metrics?.Sensors.MaxBy(sensor => sensor.Celsius);

        return new HealthReading {
            Resource = Resource,
            Level    = worst.Level,
            Cause    = worst.Cause,
            Severity = worst.Severity,
            Measures = new ThermalHealthMeasures(
                hottest?.Component ?? ThermalComponent.Other,
                hottest?.Celsius,
                metrics?.Pressure,
                limit)
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

        return new HealthCondition(level, new HealthCause(HealthCauseKind.ThermalPressure, (int)pressure), 1.0 + (int)pressure);
    }

    public HealthResource Resource => HealthResource.Thermal;
}
