namespace Task.Monitor.System.Services;

public enum ServiceStatus
{
    None = 0,
    Starting = 1,
    Running = 2,
    Stopping = 3,
    Stopped = 4,
    Errored = 5
}