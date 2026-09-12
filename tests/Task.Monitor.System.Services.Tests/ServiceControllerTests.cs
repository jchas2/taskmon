namespace Task.Monitor.System.Services.Tests;

public sealed class ServiceControllerTests
{
    private const int MinimumDelay = 500;

    // Publishes something so the controller's snapshot cycle runs (it skips while no service has
    // published anything at all).
    private sealed class FakeAlphaService : WorkerService
    {
        protected override void OnDoWork(CancellationToken cancellationToken) => Publish(new object());
    }

    private sealed class FakeBravoService : WorkerService
    {
        protected override void OnDoWork(CancellationToken cancellationToken) { }
    }

    private sealed class FaultyService : WorkerService
    {
        protected override void OnDoWork(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    [Fact]
    public void Snapshot_Lists_Every_Registered_Service_In_Registration_Order_With_Its_Status()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        SystemSnapshot? latest = null;

        controller.SystemSnapshotUpdated += (_, e) => latest = e.Snapshot;
        controller
            .AddService(() => new FakeAlphaService())
            .AddService(() => new FakeBravoService());
        controller.Start();

        Thread.Sleep(MinimumDelay * 3);
        controller.Stop();

        Assert.NotNull(latest);

        IReadOnlyList<ServiceHealth> services = latest!.Services;

        Assert.Equal(2, services.Count);
        Assert.Equal("FakeAlpha", services[0].Name);
        Assert.Equal("FakeBravo", services[1].Name);
        Assert.All(services, service => Assert.Equal(ServiceStatus.Running, service.Status));
    }

    [Fact]
    public void Snapshot_Reports_A_Service_That_Threw_As_Errored()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        SystemSnapshot? latest = null;

        controller.SystemSnapshotUpdated += (_, e) => latest = e.Snapshot;
        controller
            .AddService(() => new FakeAlphaService())
            .AddService(() => new FaultyService());
        controller.Start();

        Thread.Sleep(MinimumDelay * 3);
        controller.Stop();

        Assert.NotNull(latest);

        ServiceHealth faulty = Assert.Single(latest!.Services, service => service.Name == "Faulty");
        Assert.Equal(ServiceStatus.Errored, faulty.Status);
    }
}
