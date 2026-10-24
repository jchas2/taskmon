#if __APPLE__
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Tests.Gpu;

public sealed class GpuMemoryLimitTests
{
    private const long Gigabyte = 1024L * 1024 * 1024;

    [Fact]
    public void A_Raised_Wired_Limit_Wins()
    {
        Assert.Equal(32 * Gigabyte, GpuService.GpuMemoryLimit(32 * 1024, 30 * (ulong)Gigabyte, 8 * Gigabyte));
    }

    [Fact]
    public void Falls_Back_To_The_Allocated_Pool()
    {
        Assert.Equal(8 * Gigabyte, GpuService.GpuMemoryLimit(0, null, 8 * Gigabyte));
    }

    [Fact]
    public void Uses_The_Recommended_Working_Set_By_Default()
    {
        Assert.Equal(30 * Gigabyte, GpuService.GpuMemoryLimit(0, 30 * (ulong)Gigabyte, 8 * Gigabyte));
    }
}
#endif
