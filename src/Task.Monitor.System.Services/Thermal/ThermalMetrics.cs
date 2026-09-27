namespace Task.Monitor.System.Services.Thermal;

public sealed class ThermalMetrics
{
    public List<ThermalSensor> Sensors { get; set; } = new();
}
