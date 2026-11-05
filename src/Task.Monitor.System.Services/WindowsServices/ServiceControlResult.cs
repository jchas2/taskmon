namespace Task.Monitor.System.Services.WindowsServices;

public enum ServiceControlResult
{
    Succeeded,
    AccessDenied,
    Busy,
    CannotStop,
    DependencyFailed,
    DependentsRunning,
    DidNotStop,
    Disabled,
    Failed,
    NotFound,
    NotSupported,
    StillPending,
    StoppedAfterStarting,
    TimedOut
}
