using Task.Monitor.System.Services.Health.Rules;

namespace Task.Monitor.System.Services.Health;

public sealed class HealthEvaluator
{
    private static readonly IHealthRule[] Rules = [
        new CpuHealthRule(),
        new MemoryHealthRule(),
        new DiskHealthRule(),
        new VolumesHealthRule(),
        new GpuHealthRule(),
        new NetworkHealthRule(),
        new ThermalHealthRule(),
        new AppsHealthRule()
    ];

    private readonly List<HealthEvent> changedEvents = new();
    private readonly List<HealthEvent> closedEvents = new();
    private readonly HealthCulpritFinder culprits = new();

    private readonly Dictionary<HealthResource, HealthTracker> trackers = Rules.ToDictionary(
        rule => rule.Resource,
        _ => new HealthTracker());

    private IReadOnlyList<HealthEvent> BuildEvents(HealthThresholds thresholds) =>
        trackers.Values
            .Select(tracker => tracker.OpenEvent)
            .OfType<HealthEvent>()
            .Concat(closedEvents)
            .OrderByDescending(healthEvent => healthEvent.StartUtc)
            .Take(thresholds.EventHistorySize)
            .ToList();

    private static HealthReading BuildReading(HealthReading reading, HealthTracker tracker) =>
        reading with {
            Level = tracker.Level,
            Cause = tracker.Level >= HealthLevel.Warning ? tracker.Cause : null
        };

    public HealthInfo Evaluate(HealthSample sample, HealthThresholds thresholds, DateTime utcNow)
    {
        culprits.Update(sample.Processes, utcNow);

        HealthReading[] readings = Rules.Select(rule => rule.Evaluate(sample, thresholds)).ToArray();

        HealthResource[] pressured = readings
            .Where(reading => reading.Level >= HealthLevel.Warning)
            .Select(reading => reading.Resource)
            .ToArray();

        IReadOnlyList<HealthCulprit> hung  = HungCulprits(sample, thresholds);
        IReadOnlyList<HealthCulprit> found = [.. hung, .. culprits.Find(pressured)];

        foreach (HealthReading reading in readings) {
            HealthTracker tracker  = trackers[reading.Resource];
            HealthEvent?  previous = tracker.OpenEvent;
            HealthEvent?  closed   = tracker.Update(reading, thresholds, utcNow, NamesFor(found, reading.Resource));

            if (closed != null) {
                closedEvents.Add(closed);
                changedEvents.Add(closed);
            }

            if (tracker.OpenEvent is { } open && open.Level != previous?.Level) {
                changedEvents.Add(open);
            }
        }

        Trim(thresholds);

        HealthReading[] settled = readings
            .Select(reading => BuildReading(reading, trackers[reading.Resource]))
            .ToArray();

        return new HealthInfo {
            Level     = settled.Max(reading => reading.Level),
            SinceUtc  = OverallSince(),
            Resources = settled,
            Culprits  = [.. hung, .. culprits.Find(readings
                .Where(reading => trackers[reading.Resource].IsUnderPressure(reading, thresholds, utcNow))
                .Select(reading => reading.Resource))],
            Events    = BuildEvents(thresholds),
            Reports   = sample.Reports
        };
    }

    public IReadOnlyList<HealthEvent> Finish(DateTime utcNow) =>
        trackers.Values
            .Select(tracker => tracker.OpenEvent)
            .OfType<HealthEvent>()
            .Select(open => open with { EndUtc = utcNow })
            .ToList();

    private static IReadOnlyList<HealthCulprit> HungCulprits(HealthSample sample, HealthThresholds thresholds) =>
        sample.HungApps
            .Where(app => app.Seconds >= thresholds.NotRespondingWarningSeconds)
            .Select(app => new HealthCulprit {
                ProcessName          = app.Name,
                Pid                  = app.Pid,
                Resources            = [HealthResource.Apps],
                NotRespondingSeconds = app.Seconds
            })
            .ToList();

    private static IReadOnlyList<string> NamesFor(IReadOnlyList<HealthCulprit> found, HealthResource resource) =>
        found
            .Where(culprit => culprit.Resources.Contains(resource))
            .Select(culprit => culprit.ProcessName)
            .ToList();

    private DateTime? OverallSince() =>
        trackers.Values
            .Select(tracker => tracker.OpenEvent?.StartUtc)
            .Where(start => start != null)
            .Min();

    public void Restore(IEnumerable<HealthEvent> saved) => closedEvents.AddRange(saved);

    public IReadOnlyList<HealthEvent> TakeChanges()
    {
        List<HealthEvent> changes = [.. changedEvents];

        changedEvents.Clear();
        return changes;
    }

    private void Trim(HealthThresholds thresholds)
    {
        if (closedEvents.Count > thresholds.EventHistorySize) {
            closedEvents.RemoveRange(0, closedEvents.Count - thresholds.EventHistorySize);
        }
    }
}
