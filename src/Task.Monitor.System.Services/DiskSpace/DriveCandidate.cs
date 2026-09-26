namespace Task.Monitor.System.Services.DiskSpace;

public readonly record struct DriveCandidate(
    string RootPath, 
    bool IsReady, 
    DriveType DriveType);
