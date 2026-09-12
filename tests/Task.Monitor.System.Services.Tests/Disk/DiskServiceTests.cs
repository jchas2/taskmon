using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Tests.Disk;

public sealed class DiskServiceTests
{
    [Fact]
    public void Should_Run_DiskSystemService()
    {
        DiskService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }
}
