using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.System.Services.Tests.DiskSpace;

public sealed class DiskSpaceServiceTests : IDisposable
{
    private const int MinimumDelay = 500;

    private readonly string root;

    public DiskSpaceServiceTests()
    {
        root = Path.Combine(Path.GetTempPath(), "DiskSpaceServiceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "a.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(root, "b.bin"), new byte[50]);
    }

    public void Dispose()
    {
        try {
            Directory.Delete(root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
        }
    }

    [Fact]
    public void Should_Run_DiskSpaceService()
    {
        DiskSpaceService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }

    [Fact]
    public void Should_Publish_Idle_State_Before_Any_Scan_Starts()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        SystemSnapshot? latest = null;

        controller.SystemSnapshotUpdated += (_, e) => latest = e.Snapshot;
        controller.AddService(() => new DiskSpaceService { Delay = MinimumDelay });
        controller.Start();

        Thread.Sleep(MinimumDelay * 2);
        controller.Stop();

        Assert.NotNull(latest);
        Assert.NotNull(latest!.DiskSpace);
        Assert.Equal(DiskSpaceScanState.Idle, latest.DiskSpace!.Specs.State);
    }

    [Fact]
    public void StartScan_Publishes_A_Completed_Scan_Of_A_Real_Directory_Through_The_Controller()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        SystemSnapshot? latest = null;

        controller.SystemSnapshotUpdated += (_, e) => latest = e.Snapshot;
        controller.AddService(() => new DiskSpaceService { Delay = MinimumDelay });
        controller.Start();

        controller.GetService<DiskSpaceService>().StartScan(root);

        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (DateTime.UtcNow < deadline &&
               latest?.DiskSpace?.Specs.State != DiskSpaceScanState.Completed) {
            Thread.Sleep(25);
        }

        controller.Stop();

        DiskSpaceSpecs? specs = latest?.DiskSpace?.Specs;

        Assert.NotNull(specs);
        Assert.Equal(DiskSpaceScanState.Completed, specs!.State);
        Assert.Equal(150, specs.TotalBytesScanned);
        Assert.Equal(2, specs.FilesScanned);
    }

    [Fact]
    public void CancelScan_Without_An_Active_Scan_Does_Not_Throw()
    {
        DiskSpaceService service = new();
        service.CancelScan();
    }
}
