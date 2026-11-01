using Task.Monitor.System.Services.Network;

namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class NetworkHealthRule : IHealthRule
{
    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Network?.Metrics is not { } metrics) {
            return new HealthReading { Resource = Resource };
        }

        double errors = metrics.ErrorsPerSecond;

        // Any rise counts, so this one has no threshold: a healthy link doesn't lose packets.
        HealthCondition condition = errors > 0.0
            ? new HealthCondition(HealthLevel.Warning, $"{errors:0.#} errors/s", 1.0 + errors)
            : HealthCondition.Clear;

        return new HealthReading {
            Resource    = Resource,
            Level       = condition.Level,
            Reason      = condition.Reason,
            Severity    = condition.Severity,
            Utilisation = $"↓ {metrics.ReceiveMegabytesPerSecond:0.0} ↑ {metrics.SendMegabytesPerSecond:0.0} MB/s",
            Errors      = $"{errors:0.#} errors/s"
        };
    }

    public HealthResource Resource => HealthResource.Network;
}
