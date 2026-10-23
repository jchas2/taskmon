using Task.Monitor.Extensions;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.Tests.Extensions;

public sealed class SystemSnapshotExtensionsTests
{
    private static readonly SystemSnapshot Snapshot = new() {
        Gpu  = new GpuInfo  { Specs = new GpuSpecs  { Devices = [new GpuDevice  { AdapterLuid = 111, Description = "Apple GPU" }] } },
        Disk = new DiskInfo { Specs = new DiskSpecs { Devices = [new DiskDevice { Index = 0, Model = "APPLE SSD" }] } }
    };

    [Fact]
    public void ToDiskLabel_Falls_Back_To_The_Index_Without_A_Matching_Drive()
    {
        Assert.Equal("Disk 3", Snapshot.ToDiskLabel("3"));
        Assert.Equal("Disk 0", ((SystemSnapshot?)null).ToDiskLabel("0"));
    }

    [Fact]
    public void ToDiskLabel_Names_The_Matching_Drive()
    {
        Assert.Equal("Disk 0 · APPLE SSD", Snapshot.ToDiskLabel("0"));
    }

    [Fact]
    public void ToGpuLabel_Falls_Back_Without_A_Matching_Adapter()
    {
        Assert.Equal("GPU", Snapshot.ToGpuLabel("222"));
        Assert.Equal("GPU", Snapshot.ToGpuLabel("nvml0"));
        Assert.Equal("GPU", ((SystemSnapshot?)null).ToGpuLabel("111"));
    }

    [Fact]
    public void ToGpuLabel_Names_The_Matching_Adapter()
    {
        Assert.Equal("GPU · Apple GPU", Snapshot.ToGpuLabel("111"));
    }
}
