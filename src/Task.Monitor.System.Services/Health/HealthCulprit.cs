namespace Task.Monitor.System.Services.Health;

public sealed record HealthCulprit
{
    public required string               ProcessName  { get; init; }
    public int                           ProcessCount { get; init; } = 1;
    public int                           Pid          { get; init; }
    public IReadOnlyList<HealthResource> Resources    { get; init; } = [];

    public double? CpuPercent                 { get; init; }
    public long?   MemoryBytes                { get; init; }
    public double? MemoryGrowthBytesPerMinute { get; init; }
    public double? DiskBytesPerSecond         { get; init; }
    public double? GpuPercent                 { get; init; }
    public double? NotRespondingSeconds       { get; init; }
}
