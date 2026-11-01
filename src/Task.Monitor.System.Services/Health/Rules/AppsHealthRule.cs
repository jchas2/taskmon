namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class AppsHealthRule : IHealthRule
{
    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        int reported = sample.Reports.Count(report => report.Kind is HealthReportKind.AppCrash or HealthReportKind.AppHang);

        string errors = reported > 0 ? $"{reported} app faults" : string.Empty;

        if (!sample.CanDetectHungApps) {
            return new HealthReading { Resource = Resource, Errors = errors };
        }

        HungApp? longest = sample.HungApps.MaxBy(app => app.Seconds);

        HealthCondition condition = longest != null
            ? HealthCondition.Rising(
                longest.Seconds,
                thresholds.NotRespondingWarningSeconds,
                thresholds.NotRespondingCriticalSeconds,
                $"{longest.Name} not responding for {longest.Seconds:0} s")
            : HealthCondition.Clear;

        return new HealthReading {
            Resource    = Resource,
            Level       = condition.Level,
            Reason      = condition.Reason,
            Severity    = condition.Severity,
            IsImmediate = true,
            Utilisation = $"{sample.HungApps.Count} not responding",
            Errors      = errors
        };
    }

    public HealthResource Resource => HealthResource.Apps;
}
