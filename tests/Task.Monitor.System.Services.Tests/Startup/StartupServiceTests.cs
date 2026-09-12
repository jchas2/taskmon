using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class StartupServiceTests
{
    private const int MinimumDelay = 500;

    [Fact]
    public void Should_Run_StartupService()
    {
        StartupService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }

    [Fact]
    public void Should_Publish_Startup_Entries_Through_The_Controller()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        SystemSnapshot? latest = null;

        controller.SystemSnapshotUpdated += (_, e) => latest = e.Snapshot;
        controller.AddService(() => new StartupService { Delay = MinimumDelay });
        controller.Start();

        Thread.Sleep(MinimumDelay * 3);
        controller.Stop();

        Assert.NotNull(latest);
        Assert.NotNull(latest!.Startup);

        // The exact contents depend on the host, but every entry the scan produces must be
        // well-formed, and a Windows host effectively always has at least one Run entry.
        foreach (StartupEntry entry in latest.Startup!.Specs.Entries) {
            Assert.False(string.IsNullOrWhiteSpace(entry.Name));
            Assert.True(Enum.IsDefined(entry.Source));
            Assert.True(Enum.IsDefined(entry.Scope));
            Assert.True(Enum.IsDefined(entry.State));
        }

        Assert.Contains(latest.Services, health => health.Name == "Startup");
    }
}
