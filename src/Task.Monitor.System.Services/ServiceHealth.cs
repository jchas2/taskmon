namespace Task.Monitor.System.Services;

public readonly record struct ServiceHealth(
    string Name, 
    ServiceStatus Status);
