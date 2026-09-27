namespace Task.Monitor.System.Services.Thermal;

public sealed class ThermalSensor
{
    public ThermalComponent Component { get; set; }
    public string ComponentId         { get; set; } = string.Empty;
    public string SensorName          { get; set; } = string.Empty;
    public double Celsius             { get; set; }
    public double? WarningCelsius     { get; set; }
    public double? CriticalCelsius    { get; set; }
    public ThermalSource Source       { get; set; }
    public string Key => $"{Component}:{ComponentId}:{SensorName}";
}
