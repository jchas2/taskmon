namespace Task.Monitor.System.Services.Disk;

public sealed class DiskSpecs
{
    public List<DiskDevice> Devices           { get; set; } = new();
    public List<DiskVolume> UnattachedVolumes { get; set; } = new();
}
