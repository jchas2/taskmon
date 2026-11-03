using Task.Monitor.System.Services.Network;

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

        HealthCondition worst = HealthCondition.Worst([RateCondition(errors, "errors"), RateCondition(discards, "discards")]);

        return new HealthReading {
            Resource    = Resource,
            Level       = worst.Level,
            Reason      = worst.Reason,
            Severity    = worst.Severity,
            Utilisation = $"↓ {metrics.ReceiveMegabytesPerSecond:0.0} ↑ {metrics.SendMegabytesPerSecond:0.0} MB/s",
            Saturation  = $"Discards {discards:0.#}/s",
            Errors      = $"{errors:0.#} errors/s"
        };
    }

    private static HealthCondition RateCondition(double perSecond, string name) =>
        perSecond > 0.0
            ? new HealthCondition(HealthLevel.Warning, $"{perSecond:0.#} {name}/s", 1.0 + perSecond)
            : HealthCondition.Clear;

    public HealthResource Resource => HealthResource.Network;
}
