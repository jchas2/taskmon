namespace Task.Monitor.System.Services.Health;

public sealed class HealthInfo
{
    public HealthLevel                  Level     { get; init; } = HealthLevel.Unknown;
    public DateTime?                    SinceUtc  { get; init; }
    public IReadOnlyList<HealthReading> Resources { get; init; } = [];
    public IReadOnlyList<HealthCulprit> Culprits  { get; init; } = [];
    public IReadOnlyList<HealthEvent>   Events    { get; init; } = [];
    public IReadOnlyList<HealthReport>  Reports   { get; init; } = [];
}
