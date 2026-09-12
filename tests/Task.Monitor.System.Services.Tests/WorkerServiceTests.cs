using Task.Monitor.System.Services;

namespace Task.Monitor.System.Services.Tests;

public sealed class WorkerServiceTests
{
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

        public bool WaitForCycle(int millisecondsTimeout)
        {
            bool ran = CycleRan.Wait(millisecondsTimeout);
            CycleRan.Reset();
            return ran;
        }

        public bool[] Snapshot()
        {
            lock (RefreshFlagPerCycle) {
                return RefreshFlagPerCycle.ToArray();
            }
        }
    }

    [Fact]
    public void RequestImmediateRefresh_Wakes_The_Worker_Well_Before_The_Delay()
    {
        SpyWorkerService service = new();

        // Clamped up to the 500ms minimum, so a natural next cycle is 500ms away.
        service.Delay = 100;
        service.Start();

        try {
            Assert.True(service.WaitForCycle(1000), "the first cycle should run immediately");

            service.RequestImmediateRefresh();

            Assert.True(
                service.WaitForCycle(250),
                "RequestImmediateRefresh should end the wait long before the 500ms delay");

            Assert.Contains(true, service.Snapshot());
        }
        finally {
            service.Stop();
        }
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
}
