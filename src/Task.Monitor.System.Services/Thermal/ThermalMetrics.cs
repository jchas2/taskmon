namespace Task.Monitor.System.Services.Thermal;

public sealed class ThermalMetrics
{
    public List<ThermalSensor> Sensors { get; set; } = new();
    public List<FanReading> Fans       { get; set; } = new();
    public ThermalPressure? Pressure   { get; set; }
}
