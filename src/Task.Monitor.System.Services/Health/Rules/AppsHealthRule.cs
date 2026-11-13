namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class AppsHealthRule : IHealthRule
{
    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        int faults = sample.Reports.Count(report => report.Kind is HealthReportKind.AppCrash or HealthReportKind.AppHang);

        if (!sample.CanDetectHungApps) {
            return new HealthReading { Resource = Resource, Measures = new AppsHealthMeasures(null, faults) };
        }

        HungApp? longest = sample.HungApps.MaxBy(app => app.Seconds);

        HealthCondition condition = longest != null
            ? HealthCondition.Rising(
                longest.Seconds,
                thresholds.NotRespondingWarningSeconds,
                thresholds.NotRespondingCriticalSeconds,
                new HealthCause(HealthCauseKind.AppNotResponding, longest.Seconds, longest.Name))
            : HealthCondition.Clear;

        return new HealthReading {
            Resource    = Resource,
            Level       = condition.Level,
            Cause       = condition.Cause,
            Severity    = condition.Severity,
            IsImmediate = true,
            Measures    = new AppsHealthMeasures(sample.HungApps.Count, faults)
        };
    }

    public HealthResource Resource => HealthResource.Apps;
}
