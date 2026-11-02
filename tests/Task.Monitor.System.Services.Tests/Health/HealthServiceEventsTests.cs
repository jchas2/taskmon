using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Services.Health;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class HealthServiceEventsTests : IDisposable
{
    private const int MinimumDelay = 500;

    private static readonly DateTime Start = new(2026, 11, 1, 6, 3, 33, DateTimeKind.Utc);

    private readonly string           folder = Directory.CreateTempSubdirectory("taskmon-events").FullName;
    private readonly HealthEventStore store;

    public HealthServiceEventsTests() => store = new HealthEventStore(new FileSystem(), folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void Saved_Events_Are_Listed_From_The_First_Update()
    {
        store.Save(new HealthEvent {
            Resource = HealthResource.Disk,
            Level    = HealthLevel.Warning,
            StartUtc = Start,
            EndUtc   = Start.AddMinutes(1)
        });

        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(() => new HealthService { Delay = MinimumDelay, EventStore = store });
        controller.Start();

        SystemSnapshot? latest = snapshots.WaitForUpdates(snapshot => snapshot.Health, count: 1);
        controller.Stop();

        // The service reads the real machine, which can add events of its own, like a nearly full drive.
        Assert.Contains(latest?.Health?.Events ?? [], healthEvent => healthEvent.StartUtc == Start);
    }
}
