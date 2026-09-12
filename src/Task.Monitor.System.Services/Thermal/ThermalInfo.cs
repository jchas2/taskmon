namespace Task.Monitor.System.Services.Thermal;

public sealed class ThermalInfo
{
    public ThermalMetrics Metrics { get; set; } = new();
}
