using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed partial class StartupServiceTests
{
    private const int MinimumDelay = 500;

    [Fact]
    public void Should_Publish_Startup_Entries_Through_The_Controller()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(() => new StartupService { Delay = MinimumDelay });
        controller.Start();

        SystemSnapshot? latest = snapshots.WaitFor(snapshot => snapshot.Startup is not null);
        controller.Stop();

        Assert.NotNull(latest);
        Assert.NotNull(latest.Startup);

        // The exact contents depend on the host, but every entry the scan produces must be
        // well-formed, whichever platform's sources (Run keys, launchd jobs...) produced it.
        foreach (StartupEntry entry in latest.Startup!.Specs.Entries) {
            Assert.False(string.IsNullOrWhiteSpace(entry.Name));
            Assert.True(Enum.IsDefined(entry.Source));
            Assert.True(Enum.IsDefined(entry.Scope));
            Assert.True(Enum.IsDefined(entry.State));
        }

        Assert.Contains(latest.Services, health => health.Name == "Startup");
    }

    [Fact]
    public void Should_Run_StartupService()
    {
        StartupService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }
}
