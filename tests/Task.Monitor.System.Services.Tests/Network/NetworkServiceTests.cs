using Task.Monitor.System.Services.Network;

namespace Task.Monitor.System.Services.Tests.Network;

public sealed partial class NetworkServiceTests
{
    private const int MinimumDelay = 500;

    [Fact]
    public void Should_Publish_Active_Adapters_In_Specs_Order_With_Totals_Of_Those_Counted()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(new NetworkService { Delay = MinimumDelay });
        controller.Start();

        // The first cycle primes the counters, so rates arrive from the third.
        SystemSnapshot? latest = snapshots.WaitForUpdates(snapshot => snapshot.Network, count: 3);
        controller.Stop();

        Assert.NotNull(latest?.Network);

        NetworkInfo network = latest!.Network!;

        List<ulong> activeInOrder = network.Specs.Devices
            .Where(device => device.IsActive)
            .Select(device => device.InterfaceLuid)
            .ToList();

        List<ulong> published = network.Metrics.Devices.Select(device => device.InterfaceLuid).ToList();

        Assert.Equal(activeInOrder.Where(published.Contains), published);

        HashSet<ulong> counted = network.Specs.Devices
            .Where(device => device.CountsTowardAggregate)
            .Select(device => device.InterfaceLuid)
            .ToHashSet();

        IEnumerable<NetworkDeviceMetrics> countedDevices = network.Metrics.Devices.Where(device => counted.Contains(device.InterfaceLuid));

        Assert.Equal(countedDevices.Aggregate(0UL, (total, device) => total + device.TotalBytesReceived), network.Metrics.TotalBytesReceived);
        Assert.Equal(countedDevices.Sum(device => device.SendBytesPerSecond), network.Metrics.SendBytesPerSecond, precision: 6);
        Assert.All(network.Metrics.Devices, device => Assert.True(device.ReceiveBytesPerSecond >= 0 && device.ErrorsPerSecond >= 0));
    }

    [Fact]
    public void Should_Run_NetworkSystemService()
    {
        NetworkService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }
}
