namespace Task.Monitor.System.Services.Process;

// The Service Control Manager's current-state value for a service, mirroring the Services
// snap-in's Status column.
public enum WindowsServiceStatus
{
    Stopped,
    StartPending,
    StopPending,
    Running,
    ContinuePending,
    PausePending,
    Paused
}
