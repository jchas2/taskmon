using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Health;

public sealed record ThermalHealthMeasures(
    ThermalComponent HottestComponent,
    double?          HottestCelsius,
    ThermalPressure? Pressure,
    double?          PerformanceLimitPercent) : HealthMeasures;
