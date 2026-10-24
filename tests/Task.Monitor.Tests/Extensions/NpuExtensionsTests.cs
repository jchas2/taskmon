using Task.Monitor.Extensions;
using Task.Monitor.System.Services.Npu;

namespace Task.Monitor.Tests.Extensions;

public sealed class NpuExtensionsTests
{
    [Fact]
    public void ToDisplayName_Labels_The_Device_As_An_Indexed_Npu()
    {
        Assert.Equal("NPU 0  Apple Neural Engine", new NpuDevice { Index = 0, Description = "Apple Neural Engine" }.ToDisplayName());
        Assert.Equal("NPU 1", new NpuDevice { Index = 1 }.ToDisplayName());
    }

    [Fact]
    public void ToNpuCoresText_Counts_The_Cores()
    {
        Assert.Equal("16 cores", new NpuDevice { Cores = 16 }.ToNpuCoresText());
    }

    [Fact]
    public void ToNpuPercentage_Matches_The_Gpu_Format()
    {
        Assert.Equal($"{0.125:000.0%}", new NpuDeviceMetrics { ActiveRatio = 0.125 }.ToNpuPercentage());
    }
}
