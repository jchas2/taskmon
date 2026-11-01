namespace Task.Monitor.System.Services.Health;

// One episode of a resource under pressure, from when the condition began until it cleared.
public sealed record HealthEvent
{
    public required HealthResource Resource   { get; init; }
    public required HealthLevel    Level      { get; init; }
    public required DateTime       StartUtc   { get; init; }
    public DateTime?               EndUtc     { get; init; }
    public string                  PeakReason { get; init; } = string.Empty;
    public double                  PeakValue  { get; init; }
    public IReadOnlyList<string>   Culprits   { get; init; } = [];
}
