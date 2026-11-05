namespace Task.Monitor.System.Services.WindowsServices;

public sealed record ServiceControlOutcome(
    ServiceControlResult   Result,
    int                    Win32Error = 0,
    IReadOnlyList<string>? Dependents = null);
