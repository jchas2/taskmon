using Task.Monitor.System.Services.Memory;

namespace Task.Monitor.System.Services.Health;

public sealed record MemoryHealthMeasures(
    double          InUsePercent,
    double?         CommitPercent,
    double?         PagesInPerSecond,
    MemoryPressure? Pressure) : HealthMeasures;
