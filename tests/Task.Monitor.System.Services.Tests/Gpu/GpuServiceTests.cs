using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Tests.Gpu;

public sealed class GpuServiceTests
{
    [Fact]
    public void Should_Run_GpuSystemService()
    {
        GpuService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }
}
