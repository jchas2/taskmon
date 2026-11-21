#if __WIN32__
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed partial class ProcessServiceTests
{
    [Fact]
    public void Should_Mark_Service_Processes_From_The_Published_Services()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(new WindowsServicesService { Delay = MinimumDelay });
        controller.AddService(new ProcessService { Delay = MinimumDelay });
        controller.Start();

        // ProcessService maps the list published before its cycle, so its first cycle may have none.
        SystemSnapshot? latest = snapshots.WaitFor(snapshot =>
            snapshot.WindowsServices is not null &&
            snapshot.Processes?.Metrics.Entries.Any(entry => entry.IsDaemon) == true);

        controller.Stop();

        Assert.NotNull(latest?.Processes);
        Assert.NotNull(latest!.WindowsServices);

        Dictionary<int, WindowsServiceInfo> services = WindowsServicesService.MapProcesses(latest.WindowsServices.Specs.Services);

        Assert.All(latest.Processes!.Metrics.Entries.Where(entry => entry.IsDaemon), entry => {
            WindowsServiceInfo service = Assert.Contains(entry.Pid, (IDictionary<int, WindowsServiceInfo>)services);

            Assert.Equal(service.DisplayName, entry.FileDescription);
        });
    }
}
#endif
