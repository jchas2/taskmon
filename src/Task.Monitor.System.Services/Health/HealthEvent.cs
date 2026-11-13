namespace Task.Monitor.System.Services.Health;

public sealed record HealthEvent
{
    public required HealthResource Resource      { get; init; }
    public required HealthLevel    Level         { get; init; }
    public required DateTime       StartUtc      { get; init; }
    public DateTime?               EndUtc        { get; init; }
    public bool                    IsInterrupted { get; init; }
    public HealthCause?            PeakCause     { get; init; }
    public double                  PeakValue     { get; init; }
    public IReadOnlyList<string>   Culprits      { get; init; } = [];
}
