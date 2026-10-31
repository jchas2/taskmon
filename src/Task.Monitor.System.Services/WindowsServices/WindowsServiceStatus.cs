namespace Task.Monitor.System.Services.WindowsServices;

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
