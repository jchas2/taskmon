namespace Task.Monitor.System.Services.Disk;

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
    public long   Capacity         { get; set; }
    public List<DiskVolume> Volumes { get; } = new();
}
