namespace Task.Monitor.System.Services.Disk;

public sealed class DiskDevice
{
    public int    Index            { get; set; }
    public string Manufacturer     { get; set; } = Constants.NotAvailable;
    public string Model            { get; set; } = Constants.NotAvailable;
    public string FirmwareRevision { get; set; } = Constants.NotAvailable;
    public string SerialNumber     { get; set; } = Constants.NotAvailable;
    public string BusType          { get; set; } = Constants.NotAvailable;
    public string MediaType        { get; set; } = Constants.NotAvailable;
    public bool   IsRemovable      { get; set; }
    public long   Capacity         { get; set; }
    public List<DiskVolume> Volumes { get; } = new();
}
