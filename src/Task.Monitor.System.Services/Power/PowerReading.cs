namespace Task.Monitor.System.Services.Power;

public sealed class PowerReading
{
    public PowerComponent Component { get; set; }

    // Empty for the CPU / system; the GpuDevice.AdapterLuid for a GPU; the DiskDevice.Index for a
    // disk - the same join keys the other services use.
    public string ComponentId { get; set; } = string.Empty;

    // The rail this measures: "GPU", "Package", "System".
    public string Rail { get; set; } = string.Empty;

    public double Watts { get; set; }

    // True when this is a nameplate figure (an NVMe drive's peak power state), not a live draw.
    public bool IsRated { get; set; }

    public PowerSource Source { get; set; }
}
