namespace Task.Monitor.System.Services.Soc;

public sealed class SocMetrics
{
    public double? GpuFrequencyMhz         { get; set; }
    public double? GpuMaxFrequencyMhz      { get; set; }
    public double? GpuThermalLimitRatio    { get; set; }
    public double? GpuPowerLimitRatio      { get; set; }
}
