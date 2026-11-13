namespace Task.Monitor.System.Services.Health;

public sealed record HealthReading
{
    public required HealthResource Resource    { get; init; }
    public HealthLevel             Level       { get; init; } = HealthLevel.Unknown;
    public HealthCause?            Cause       { get; init; }
    public double                  Severity    { get; init; }
    public bool                    IsImmediate { get; init; }
    public HealthMeasures?         Measures    { get; init; }
}
