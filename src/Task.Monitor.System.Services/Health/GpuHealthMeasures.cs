namespace Task.Monitor.System.Services.Health;

public sealed record GpuHealthMeasures(
    double  BusyPercent,
    double? MemoryUsedPercent,
    bool    IsDedicatedMemory) : HealthMeasures;
