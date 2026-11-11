using Task.Monitor.System.Services.Network;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Services.Tests.Network;

public sealed partial class NetworkServiceTests
{
    private static NetworkSpecs Adapter(string name, string status, ulong linkSpeed) => new() {
        Devices = [
            new NetworkDevice {
                FriendlyName      = name,
                Description       = "Test adapter",
                ConnectionType    = "Ethernet",
                PhysicalMedium    = "802.3",
                OperationalStatus = status,
                TransmitLinkSpeed = linkSpeed
            }
        ]
    };

    // A Wi-Fi link rate moves all the time, and the specs refresh on a timer.
    [Fact]
    public void LogAdapters_Ignores_A_Link_Speed_Change()
    {
        string         name    = $"Adapter {Guid.NewGuid()}";
        NetworkService service = new();

        using TraceCapture capture = new();

        service.LogAdapters(Adapter(name, "Up", 1_000_000_000), atStartup: true);
        service.LogAdapters(Adapter(name, "Up", 500_000_000), atStartup: false);

        Assert.Equal(
            [$"NetworkService: adapter {name}, Test adapter, Ethernet, 802.3, Up, 1000 Mb/s"],
            capture.LinesContaining(name));
    }

    [Fact]
    public void LogAdapters_Writes_Again_When_An_Adapter_Changes_State()
    {
        string         name    = $"Adapter {Guid.NewGuid()}";
        NetworkService service = new();

        using TraceCapture capture = new();

        service.LogAdapters(Adapter(name, "Up", 1_000_000_000), atStartup: true);
        service.LogAdapters(Adapter(name, "Down", 0), atStartup: false);

        Assert.Equal(
            [
                $"NetworkService: adapter {name}, Test adapter, Ethernet, 802.3, Up, 1000 Mb/s",
                $"NetworkService: adapter {name}, Test adapter, Ethernet, 802.3, Down, 0 Mb/s"
            ],
            capture.LinesContaining(name));
    }
}
