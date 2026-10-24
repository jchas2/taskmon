namespace Task.Monitor.System.Services.Thermal;

public sealed class FanReading
{
    public string Name   { get; set; } = string.Empty;
    public double Rpm    { get; set; }
    public double MinRpm { get; set; }
    public double MaxRpm { get; set; }
}
