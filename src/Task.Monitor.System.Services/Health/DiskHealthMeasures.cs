namespace Task.Monitor.System.Services.Health;

public sealed record DiskHealthMeasures(
    double  ActivePercent,
    double? LatencyMs,
    double? QueueLength,
    int?    WornPercent,
    ulong   MediaErrors,
    int     LoggedErrors) : HealthMeasures;
