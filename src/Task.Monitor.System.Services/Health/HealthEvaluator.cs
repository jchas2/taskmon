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
            Level  = tracker.Level,
            Reason = tracker.Level >= HealthLevel.Warning ? tracker.Reason : string.Empty
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
            HealthTracker tracker = trackers[reading.Resource];
            HealthEvent?  closed  = tracker.Update(reading, thresholds, utcNow, NamesFor(found, reading.Resource));

            if (closed != null) {
                closedEvents.Add(closed);
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
            Culprits  = [.. hung, .. culprits.Find(settled
                .Where(reading => reading.Level >= HealthLevel.Warning)
                .Select(reading => reading.Resource))],
            Events    = BuildEvents(thresholds),
            Reports   = sample.Reports
        };
    }

    // A hung app is its own cause, as it may be using little resources, if any.
    private static IReadOnlyList<HealthCulprit> HungCulprits(HealthSample sample, HealthThresholds thresholds) =>
        sample.HungApps
            .Where(app => app.Seconds >= thresholds.NotRespondingWarningSeconds)
            .Select(app => new HealthCulprit(app.Name, 1, app.Pid, $"Not responding for {app.Seconds:0} s", [HealthResource.Apps]))
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

    private void Trim(HealthThresholds thresholds)
    {
        if (closedEvents.Count > thresholds.EventHistorySize) {
            closedEvents.RemoveRange(0, closedEvents.Count - thresholds.EventHistorySize);
        }
    }
}
