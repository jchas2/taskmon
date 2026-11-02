namespace Task.Monitor.System.Services.Health;

public sealed record HealthVolume(
    string  Name, 
    long    FreeBytes, 
    long    TotalBytes, 
    bool    IsSystem,
    double? FillBytesPerMinute = null);

