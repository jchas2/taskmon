namespace Task.Monitor.System.Services.Health.Rules;

internal readonly record struct HealthCondition(HealthLevel Level, string Reason, double Severity)
{
    private const double SmallestValue = 0.001;

    public static HealthCondition Clear => new(HealthLevel.Ok, string.Empty, 0.0);

    public static HealthCondition Falling(double value, double warning, double critical, string reason)
    {
        HealthLevel level = value <= critical ? HealthLevel.Critical
                          : value <= warning  ? HealthLevel.Warning
                                              : HealthLevel.Ok;

        return From(level, reason, Math.Max(warning, SmallestValue) / Math.Max(value, SmallestValue));
    }

    private static HealthCondition From(HealthLevel level, string reason, double severity) =>
        level >= HealthLevel.Warning ? new HealthCondition(level, reason, severity) : Clear;

    public static HealthCondition Rising(double value, double warning, double critical, string reason)
    {
        HealthLevel level = value >= critical ? HealthLevel.Critical
                          : value >= warning  ? HealthLevel.Warning
                                              : HealthLevel.Ok;

        return From(level, reason, value / Math.Max(warning, SmallestValue));
    }

    public static HealthCondition Worst(IEnumerable<HealthCondition> conditions) =>
        conditions
            .OrderByDescending(condition => condition.Level)
            .ThenByDescending(condition => condition.Severity)
            .FirstOrDefault(Clear);
}
