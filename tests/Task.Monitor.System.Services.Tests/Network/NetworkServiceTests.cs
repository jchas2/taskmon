using Task.Monitor.System.Services.Network;

namespace Task.Monitor.System.Services.Tests.Network;

public sealed class NetworkServiceTests
{
    [Fact]
    public void Should_Run_NetworkSystemService()
    {
        NetworkService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }
}
