namespace Task.Monitor.System.Services.Thermal;

public sealed class ThermalSensor
{
    public ThermalComponent Component { get; set; }

    // Identifies which instance of the component this belongs to, so a performance panel can match
    // it: empty for the CPU, the GpuDevice.AdapterLuid for a GPU, the DiskDevice.Index for a disk.
    public string ComponentId { get; set; } = string.Empty;

    // The label the source gives this sensor: "Package", "GPU", "Hot Spot", "Memory Junction",
    // "Composite", "Sensor 1", "Zone 0" ...
    public string SensorName { get; set; } = string.Empty;

    public double Celsius { get; set; }

    // Thresholds the source reports, when it reports them: the point the component starts to
    // throttle, and the point it shuts down. Null when unknown.
    public double? WarningCelsius { get; set; }
    public double? CriticalCelsius { get; set; }

    public ThermalSource Source { get; set; }

    // A stable key for this sensor across samples, used to keep a chart's history attached to it.
    public string Key => $"{Component}:{ComponentId}:{SensorName}";
}
