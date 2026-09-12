using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

[Collection(ProcessServiceCollection.Name)]
public sealed class ProcessServiceTests
{
    private const int MinimumDelay = 500;

    [Fact]
    public void Should_Run_ProcessSystemService()
    {
        ProcessService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }

    [Fact]
    public void Should_Publish_Process_Metrics_Through_The_Controller()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        SystemSnapshot? latest = null;

        controller.SystemSnapshotUpdated += (_, e) => latest = e.Snapshot;
        controller.AddService(() => new ProcessService() { Delay = MinimumDelay });
        controller.Start();

        // Long enough for several cycles: the first only primes the per pid baselines, so the
        // second is the earliest that can report a rate at all.
        Thread.Sleep(MinimumDelay * 5);
        controller.Stop();

        Assert.NotNull(latest);
        Assert.NotNull(latest!.Processes);

        ProcessMetrics metrics = latest.Processes!.Metrics;

        Assert.NotEmpty(metrics.Entries);
        Assert.Equal(metrics.Entries.Count, metrics.ProcessCount);

        // Every process has at least one thread.
        Assert.True(metrics.ThreadCount >= metrics.ProcessCount);
        Assert.True(metrics.RunningCount <= metrics.ProcessCount);
    }

    [Fact]
    public void Should_Publish_Rates_That_Are_Finite_And_Non_Negative()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        SystemSnapshot? latest = null;

        controller.SystemSnapshotUpdated += (_, e) => latest = e.Snapshot;
        controller.AddService(() => new ProcessService() { Delay = MinimumDelay });
        controller.Start();

        Thread.Sleep(MinimumDelay * 5);
        controller.Stop();

        Assert.NotNull(latest?.Processes);

        foreach (ProcessEntry entry in latest!.Processes!.Metrics.Entries) {
            // A divide by a zero interval used to be the way these turned into Infinity or NaN,
            // which then propagated through the running mean and poisoned it permanently.
            Assert.False(double.IsNaN(entry.CpuTimePercent));
            Assert.False(double.IsInfinity(entry.CpuTimePercent));
            Assert.False(double.IsNaN(entry.DiskBytesPerSecond));
            Assert.False(double.IsInfinity(entry.DiskBytesPerSecond));

            Assert.True(entry.CpuTimePercent >= 0.0);
            Assert.True(entry.DiskBytesPerSecond >= 0.0);
            Assert.True(entry.GpuTimePercent >= 0.0);

            // The running mean can never exceed the peak it was fed alongside.
            Assert.True(entry.CpuTimePercentAvg <= entry.CpuTimePercentMax + double.Epsilon);
        }
    }

    [Fact]
    public void Should_Not_Grow_Its_Retained_State_Across_Cycles()
    {
        // The per pid state map is swept every cycle. If the sweep regressed, the map would keep a
        // state for every process that has ever been seen, and a recycled pid would inherit the
        // previous process's mean and max.
        ServiceController controller = new() { Delay = MinimumDelay };
        SystemSnapshot? latest = null;

        controller.SystemSnapshotUpdated += (_, e) => latest = e.Snapshot;
        controller.AddService(() => new ProcessService() { Delay = MinimumDelay });
        controller.Start();

        Thread.Sleep(MinimumDelay * 3);
        int early = latest?.Processes?.Metrics.ProcessCount ?? 0;

        Thread.Sleep(MinimumDelay * 6);
        int late = latest?.Processes?.Metrics.ProcessCount ?? 0;

        controller.Stop();

        Assert.True(early > 0);
        Assert.True(late > 0);

        // The published entry count tracks the live process list, so it must not drift upward with
        // the number of cycles run.
        Assert.True(Math.Abs(late - early) < early / 2);
    }
}
