namespace Task.Monitor.System.Services.Drivers;

public enum DriverStatus
{
    Stopped,
    StartPending,
    StopPending,
    Running,
    ContinuePending,
    PausePending,
    Paused,
    Loaded,
    NotLoaded
}
