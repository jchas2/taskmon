namespace Task.Monitor.System.Services.Health.Rules;

internal interface IHealthRule
{
    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds);

    public HealthResource Resource { get; }
}
