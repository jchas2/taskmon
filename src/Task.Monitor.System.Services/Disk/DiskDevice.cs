namespace Task.Monitor.System.Services.Disk;

// One installed physical drive, as reported by IOCTL_STORAGE_QUERY_PROPERTY against
// \\.\PhysicalDriveN. Index is the same drive number that the PhysicalDisk performance counter
// instances lead with, which is how metrics are matched back to a device.
public sealed class DiskDevice
{
    public int    Index            { get; set; }
    public string Manufacturer     { get; set; } = DiskDeviceParser.NotAvailable;
    public string Model            { get; set; } = DiskDeviceParser.NotAvailable;
    public string FirmwareRevision { get; set; } = DiskDeviceParser.NotAvailable;
    public string SerialNumber     { get; set; } = DiskDeviceParser.NotAvailable;
    public string BusType          { get; set; } = DiskDeviceParser.NotAvailable;
    public string MediaType        { get; set; } = DiskDeviceParser.NotAvailable;
    public bool   IsRemovable      { get; set; }

    // Raw capacity the drive reports, which is larger than the sum of the formatted volume
    // capacities it hosts.
    public long   Capacity         { get; set; }

    public List<DiskVolume> Volumes { get; } = new();
}
