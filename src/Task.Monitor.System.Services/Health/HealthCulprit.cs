namespace Task.Monitor.System.Services.Health;

public sealed record HealthCulprit(
    string                        ProcessName,
    int                           ProcessCount,
    int                           Pid,
    string                        Detail,
    IReadOnlyList<HealthResource> Resources);
