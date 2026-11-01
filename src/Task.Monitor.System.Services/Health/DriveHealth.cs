namespace Task.Monitor.System.Services.Health;

public sealed record DriveHealth(
    string Name, 
    byte   CriticalWarning, 
    int    PercentUsed, 
    ulong  MediaErrors);
