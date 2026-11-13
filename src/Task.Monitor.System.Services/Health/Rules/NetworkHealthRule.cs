namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class NetworkHealthRule : IHealthRule
{
    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Network?.Metrics is not { } metrics) {
            return new HealthReading { Resource = Resource };
        }

        double discards = metrics.DiscardsPerSecond;
        double errors   = metrics.ErrorsPerSecond;

        HealthCondition worst = HealthCondition.Worst([
            RateCondition(errors, HealthCauseKind.NetworkErrors),
            RateCondition(discards, HealthCauseKind.NetworkDiscards)
        ]);

        return new HealthReading {
            Resource = Resource,
            Level    = worst.Level,
            Cause    = worst.Cause,
            Severity = worst.Severity,
            Measures = new NetworkHealthMeasures(
                metrics.ReceiveBytesPerSecond,
                metrics.SendBytesPerSecond,
                discards,
                errors)
        };
    }

    private static HealthCondition RateCondition(double perSecond, HealthCauseKind kind) =>
        perSecond > 0.0
            ? new HealthCondition(HealthLevel.Warning, new HealthCause(kind, perSecond), 1.0 + perSecond)
            : HealthCondition.Clear;

    public HealthResource Resource => HealthResource.Network;
}
