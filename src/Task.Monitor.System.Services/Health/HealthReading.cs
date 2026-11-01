namespace Task.Monitor.System.Services.Health;

// One resource's row: what its rule read, and in a published HealthInfo the level it has settled at.
public sealed record HealthReading
{
    public required HealthResource Resource    { get; init; }
    public HealthLevel             Level       { get; init; } = HealthLevel.Unknown;
    public string                  Reason      { get; init; } = string.Empty;
    public double                  Severity    { get; init; }
    public bool                    IsImmediate { get; init; }
    public string                  Utilisation { get; init; } = string.Empty;
    public string                  Saturation  { get; init; } = string.Empty;
    public string                  Errors      { get; init; } = string.Empty;
}
