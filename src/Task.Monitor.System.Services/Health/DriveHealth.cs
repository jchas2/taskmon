namespace Task.Monitor.System.Services.Health;

public sealed record DriveHealth(
    int   Index,
    byte  CriticalWarning,
    int   PercentUsed,
    ulong MediaErrors);
