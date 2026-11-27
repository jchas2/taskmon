using Task.Monitor.Extensions;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.Tests.Extensions;

public sealed class SystemSnapshotExtensionsTests
{
    private static readonly SystemSnapshot Snapshot = new() {
        Gpu  = new GpuInfo  { Specs = new GpuSpecs  { Devices = [new GpuDevice  { AdapterLuid = 111, Description = "Apple GPU" }] } },
        Disk = new DiskInfo { Specs = new DiskSpecs { Devices = [new DiskDevice { Index = 0, Model = "APPLE SSD" }] } }
    };

    // The title shows the total as the CPU metre does, without the kernel and user split.
    [Fact]
    public void ToCpuChartTitle_Shows_The_Total_Only() =>
        Assert.Equal("CPU 13.5%", new CpuMetrics { CpuPercentKernelTime = 0.05, CpuPercentUserTime = 0.085 }.ToCpuChartTitle());

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
    public void ToDiskName_Is_The_Model_Alone_Or_Empty()
    {
        Assert.Equal("APPLE SSD", Snapshot.ToDiskName("0"));
        Assert.Equal(string.Empty, Snapshot.ToDiskName("3"));
        Assert.Equal(string.Empty, ((SystemSnapshot?)null).ToDiskName("0"));
        Assert.Equal(string.Empty, new SystemSnapshot {
            Disk = new DiskInfo { Specs = new DiskSpecs { Devices = [new DiskDevice { Index = 1 }] } }
        }.ToDiskName("1"));
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

    [Fact]
    public void ToGpuName_Is_The_Description_Alone_Or_Empty()
    {
        Assert.Equal("Apple GPU", Snapshot.ToGpuName("111"));
        Assert.Equal(string.Empty, Snapshot.ToGpuName("222"));
        Assert.Equal(string.Empty, Snapshot.ToGpuName("nvml0"));
        Assert.Equal(string.Empty, new SystemSnapshot {
            Gpu = new GpuInfo { Specs = new GpuSpecs { Devices = [new GpuDevice { AdapterLuid = 333 }] } }
        }.ToGpuName("333"));
    }

    // A rate is averaged over the sample, so it can have a fraction. Like a byte rate, it never
    // shows below zero.
    [Theory]
    [InlineData(0.0,       "0/s")]
    [InlineData(0.4,       "0/s")]
    [InlineData(312.6,     "313/s")]
    [InlineData(1488095.0, "1,488,095/s")]
    [InlineData(-5.0,      "0/s")]
    public void ToNetworkPacketRate_Shows_Whole_Packets_A_Second(double packetsPerSecond, string expected) =>
        Assert.Equal(expected, packetsPerSecond.ToNetworkPacketRate());
}
