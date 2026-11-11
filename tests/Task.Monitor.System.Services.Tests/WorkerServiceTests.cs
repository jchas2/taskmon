using System.Text.RegularExpressions;
using Task.Monitor.System.Services;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Services.Tests;

public sealed class WorkerServiceTests
{
    // Records IsFirstCycle for each sample, and signals once two have run.
    private sealed class FirstCycleSpyService : WorkerService
    {
        public readonly List<bool>           FirstCycleFlags = new();
        public readonly ManualResetEventSlim SecondCycleRan  = new(initialState: false);

        protected override void OnDoWork(CancellationToken cancellationToken)
        {
            lock (FirstCycleFlags) {
                FirstCycleFlags.Add(IsFirstCycle);

                if (FirstCycleFlags.Count == 2) {
                    SecondCycleRan.Set();
                }
            }
        }
    }

    private sealed class StartFailingService : WorkerService
    {
        protected override void OnStart() => throw new InvalidOperationException("boom");
    }

    private sealed class SpyWorkerService : WorkerService
    {
        public readonly List<bool> RefreshFlagPerCycle = new();
        public readonly ManualResetEventSlim CycleRan = new(initialState: false);

        protected override void OnDoWork(CancellationToken cancellationToken)
        {
            lock (RefreshFlagPerCycle) {
                RefreshFlagPerCycle.Add(ConsumeRefreshRequest());
            }

            CycleRan.Set();
        }

        public bool[] Snapshot()
        {
            lock (RefreshFlagPerCycle) {
                return RefreshFlagPerCycle.ToArray();
            }
        }

        public bool WaitForCycle(int millisecondsTimeout)
        {
            bool ran = CycleRan.Wait(millisecondsTimeout);
            CycleRan.Reset();
            return ran;
        }

        public void WakeNow() => Wake();
    }

    [Fact]
    public void ConsumeRefreshRequest_Reports_The_Change_On_Exactly_One_Cycle()
    {
        SpyWorkerService service = new();
        service.Delay = 500;
        service.Start();

        try {
            service.WaitForCycle(1000);           // first cycle
            service.RequestImmediateRefresh();
            service.WaitForCycle(500);            // woken refresh cycle
            service.WaitForCycle(1500);           // next natural cycle

            bool[] flags = service.Snapshot();

            // Somewhere in the run there is exactly one true, immediately followed by a false.
            int firstTrue = Array.IndexOf(flags, true);

            Assert.True(firstTrue >= 0, "the refresh should have been observed once");
            Assert.DoesNotContain(true, flags[(firstTrue + 1)..]);
        }
        finally {
            service.Stop();
        }
    }

    [Fact]
    public void IsFirstCycle_Is_True_Only_While_The_First_Sample_Is_Taken()
    {
        FirstCycleSpyService service = new() { Delay = 500 };
        service.Start();

        try {
            Assert.True(service.SecondCycleRan.Wait(5_000), "two cycles should run");
        }
        finally {
            service.Stop();
        }

        lock (service.FirstCycleFlags) {
            Assert.Equal([true, false], service.FirstCycleFlags.Take(2));
        }
    }

    [Fact]
    public void RequestImmediateRefresh_Wakes_The_Worker_Well_Before_The_Delay()
    {
        SpyWorkerService service = new();

        // A natural next cycle is 10s away, while a woken one runs within milliseconds. The 5s wait
        // sits between the two, so the test tells them apart even on a heavily loaded machine.
        service.Delay = 10_000;
        service.Start();

        try {
            Assert.True(service.WaitForCycle(5_000), "the first cycle should run immediately");

            service.RequestImmediateRefresh();

            Assert.True(
                service.WaitForCycle(5_000),
                "RequestImmediateRefresh should end the wait long before the 10s delay");

            Assert.Contains(true, service.Snapshot());
        }
        finally {
            service.Stop();
        }
    }

    // "started" follows the first sample, so it covers the checks a service makes there too.
    [Fact]
    public void Start_Logs_Starting_And_Then_Started_After_The_First_Sample()
    {
        using TraceCapture capture = new();

        FirstCycleSpyService service = new() { Delay = 500 };
        service.Start();

        try {
            Assert.True(service.SecondCycleRan.Wait(5_000), "two cycles should run");
        }
        finally {
            service.Stop();
        }

        IReadOnlyList<string> lines = capture.LinesContaining($"{nameof(FirstCycleSpyService)}:");

        Assert.Equal(2, lines.Count);
        Assert.Equal($"{nameof(FirstCycleSpyService)}: starting", lines[0]);
        Assert.Matches(new Regex($@"^{nameof(FirstCycleSpyService)}: started in \d+ ms, status Running$"), lines[1]);
    }

    // The exception's own lines do not say which of the services threw it.
    [Fact]
    public void Start_Names_The_Service_When_Start_Up_Throws()
    {
        using TraceCapture capture = new();

        StartFailingService service = new() { Delay = 500 };
        service.Start();

        try {
            Assert.True(
                SpinWait.SpinUntil(() => service.Status == ServiceStatus.Errored, 5_000),
                "the service should report Errored");
        }
        finally {
            service.Stop();
        }

        Assert.Equal(
            [
                $"{nameof(StartFailingService)}: starting",
                $"{nameof(StartFailingService)}: FAILED with InvalidOperationException, the service has stopped"
            ],
            capture.LinesContaining($"{nameof(StartFailingService)}:"));
    }

    [Fact]
    public void Wake_Runs_The_Next_Cycle_Early_Without_A_Refresh()
    {
        SpyWorkerService service = new();

        // As above: a natural next cycle is 10s away, a woken one runs within milliseconds.
        service.Delay = 10_000;
        service.Start();

        try {
            Assert.True(service.WaitForCycle(5_000), "the first cycle should run immediately");

            service.WakeNow();

            Assert.True(service.WaitForCycle(5_000), "Wake should end the wait long before the 10s delay");
            Assert.DoesNotContain(true, service.Snapshot());
        }
        finally {
            service.Stop();
        }
    }
}
