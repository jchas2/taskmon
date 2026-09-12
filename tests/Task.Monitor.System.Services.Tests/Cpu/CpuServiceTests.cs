using Task.Monitor.System.Services.Cpu;

namespace Task.Monitor.System.Services.Tests.Cpu;

public sealed class CpuServiceTests
{
    [Fact]
    public void Should_Run_CpuSystemService()
    {
        CpuService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }
}
