using SysThreading = System.Threading;

namespace Task.Monitor.System.Services.Tests;

// Records the snapshots a ServiceController publishes, so a test can wait for the one it needs
// rather than sleep a fixed time: a busy test run can push a service past any fixed window, and the
// test then fails for no reason of its own.
internal sealed class SnapshotRecorder : IDisposable
{
    // Generous on purpose: a wait returns as soon as it is satisfied, so this only bounds how long
    // a genuinely broken run takes to fail.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly ServiceController    controller;
    private readonly List<SystemSnapshot> snapshots = [];

    // A plain object rather than a Lock: waiting uses Monitor.Wait / PulseAll, which a Lock doesn't support.
    private readonly object gate = new();

    public SnapshotRecorder(ServiceController controller)
    {
        this.controller = controller;
        controller.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
    }

    public void Dispose() => controller.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        lock (gate) {
            snapshots.Add(e.Snapshot);
            SysThreading::Monitor.PulseAll(gate);
        }
    }

    // The first snapshot to satisfy the predicate. On timeout, the last snapshot published (null
    // if there was none), so the test's own assertions report what actually arrived.
    public SystemSnapshot? WaitFor(Func<SystemSnapshot, bool> predicate) =>
        WaitUntil(published => published.FirstOrDefault(predicate));

    // The snapshot carrying the count-th distinct value the selector picks out - e.g. the third
    // ProcessInfo, which means the process service has completed three cycles. The controller
    // republishes the latest value on every one of its own cycles, so counting snapshots alone
    // would not count service cycles. On timeout, the last snapshot published.
    public SystemSnapshot? WaitForUpdates<T>(Func<SystemSnapshot, T?> selector, int count) where T : class =>
        WaitUntil(published => {
            HashSet<T> seen = new(ReferenceEqualityComparer.Instance);

            foreach (SystemSnapshot snapshot in published) {
                if (selector(snapshot) is { } value && seen.Add(value) && seen.Count == count) {
                    return snapshot;
                }
            }

            return null;
        });

    private SystemSnapshot? WaitUntil(Func<List<SystemSnapshot>, SystemSnapshot?> find)
    {
        DateTime deadline = DateTime.UtcNow + Timeout;

        lock (gate) {
            while (true) {
                if (find(snapshots) is { } found) {
                    return found;
                }

                TimeSpan remaining = deadline - DateTime.UtcNow;

                if (remaining <= TimeSpan.Zero || !SysThreading::Monitor.Wait(gate, remaining)) {
                    return find(snapshots) ?? snapshots.LastOrDefault();
                }
            }
        }
    }
}
