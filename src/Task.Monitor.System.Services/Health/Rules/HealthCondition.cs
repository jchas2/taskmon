namespace Task.Monitor.System.Services.Health.Rules;

internal readonly record struct HealthCondition(HealthLevel Level, HealthCause? Cause, double Severity)
{
    private const double SmallestValue = 0.001;

    public static HealthCondition Clear => new(HealthLevel.Ok, null, 0.0);

    public static HealthCondition Falling(double value, double warning, double critical, HealthCause cause)
    {
        HealthLevel level = value <= critical ? HealthLevel.Critical
                          : value <= warning  ? HealthLevel.Warning
                                              : HealthLevel.Ok;

        return From(level, cause, Math.Max(warning, SmallestValue) / Math.Max(value, SmallestValue));
    }

    private static HealthCondition From(HealthLevel level, HealthCause cause, double severity) =>
        level >= HealthLevel.Warning ? new HealthCondition(level, cause, severity) : Clear;

    public static HealthCondition Rising(double value, double warning, double critical, HealthCause cause)
    {
        HealthLevel level = value >= critical ? HealthLevel.Critical
                          : value >= warning  ? HealthLevel.Warning
                                              : HealthLevel.Ok;

        return From(level, cause, value / Math.Max(warning, SmallestValue));
    }

    public static HealthCondition Worst(IEnumerable<HealthCondition> conditions) =>
        conditions
            .OrderByDescending(condition => condition.Level)
            .ThenByDescending(condition => condition.Severity)
            .FirstOrDefault(Clear);
}
