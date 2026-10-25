#if __APPLE__
using System.Net;
using Task.Monitor.System.Services.Network;
using Xunit.Abstractions;

namespace Task.Monitor.System.Services.Tests.Network;

public sealed class AdapterVisibilityTests(ITestOutputHelper output)
{
    private const int MinimumDelay = 500;

    [Fact]
    public void CompareAdapters_Puts_The_Primary_Adapter_First()
    {
        List<NetworkDevice> devices = [
            new() { InterfaceIndex = 3 },
            new() { InterfaceIndex = 12, IsPrimary = true },
            new() { InterfaceIndex = 1 },
        ];

        devices.Sort(NetworkService.CompareAdapters);

        Assert.Equal([12u, 1u, 3u], devices.Select(device => device.InterfaceIndex));
    }

    [Theory]
    // en0 on DHCP.
    [InlineData(true,  false, false, true,  "172.20.10.2,fe80::1%14", true)]
    // The system utun tunnels.
    [InlineData(true,  false, false, false, "fe80::daa9:1a48:13bf:9080%18", false)]
    // A VPN such as Tailscale.
    [InlineData(true,  false, false, false, "100.101.102.103,fd7a:115c:a1e0::1", true)]
    // AirDrop's peer link.
    [InlineData(true,  false, true,  true,  "fe80::2%11", false)]
    // Thunderbolt Bridge on a self-assigned address.
    [InlineData(true,  false, false, true,  "169.254.20.30", true)]
    // Wi-Fi without an address yet.
    [InlineData(true,  false, false, true,  "", false)]
    [InlineData(false, false, false, true,  "192.168.1.20", false)]
    [InlineData(true,  true,  false, false, "127.0.0.1", false)]
    public void IsVisibleAdapter_Hides_Self_Assigned_Software_Interfaces(
        bool isUp, bool isLoopback, bool isPeerLink, bool isPhysicalType, string addresses, bool expected)
    {
        List<IPAddress> parsed = addresses
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(IPAddress.Parse)
            .ToList();

        Assert.Equal(expected, NetworkService.IsVisibleAdapter(isUp, isLoopback, isPeerLink, isPhysicalType, parsed));
    }

    [Fact]
    public void NetworkService_Lists_Only_Routable_Or_Physical_Adapters_With_The_Primary_First()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(() => new NetworkService { Delay = MinimumDelay });
        controller.Start();

        SystemSnapshot? latest = snapshots.WaitForUpdates(snapshot => snapshot.Network, count: 3);
        controller.Stop();

        NetworkInfo network = latest!.Network!;
        List<NetworkDevice> active = network.Specs.Devices.Where(device => device.IsActive).ToList();

        foreach (NetworkDevice device in network.Specs.Devices) {
            output.WriteLine($"{device.Name} '{device.FriendlyName}' active={device.IsActive} primary={device.IsPrimary} " +
                             $"{string.Join(' ', device.IPv4Addresses.Concat(device.IPv6Addresses))}");
        }

        Assert.DoesNotContain(active, device => device.Name.StartsWith("awdl", StringComparison.Ordinal));
        Assert.All(active, device => Assert.True(
            NetworkDeviceParser.HasRoutableAddress(device.IPv4Addresses.Concat(device.IPv6Addresses).Select(IPAddress.Parse)) ||
            device.ConnectionType is "Ethernet" or "Wi-Fi"));

        if (network.Metrics.Devices.Count > 0 && network.Specs.Devices.Any(device => device.IsPrimary)) {
            NetworkDevice primary = network.Specs.Devices.Single(device => device.IsPrimary);

            Assert.Equal(primary.InterfaceIndex, network.Metrics.Devices[0].InterfaceIndex);
        }
    }
}
#endif
