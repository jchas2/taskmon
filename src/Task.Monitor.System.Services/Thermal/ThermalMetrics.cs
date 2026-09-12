namespace Task.Monitor.System.Services.Thermal;

public sealed class ThermalMetrics
{
    // Every temperature the providers could read this cycle, in no particular order. Empty on a
    // machine that exposes nothing without a kernel driver.
    public List<ThermalSensor> Sensors { get; set; } = new();
}
