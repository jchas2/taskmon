using Task.Monitor.System.Process;

namespace Task.Monitor.System.Tests.Process;

public sealed class GpuServiceTests
{
    [Fact]
    public void Should_Run_Gpu_Process_Stats()
    {
        Dictionary<int, long> data = GpuService.GetProcessStats();
        Assert.NotNull(data);
    }
}
