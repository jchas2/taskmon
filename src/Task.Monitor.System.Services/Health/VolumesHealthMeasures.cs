namespace Task.Monitor.System.Services.Health;

public sealed record VolumesHealthMeasures(
    string  Name,
    double  FullPercent,
    long    FreeBytes,
    double? FillBytesPerMinute) : HealthMeasures;
