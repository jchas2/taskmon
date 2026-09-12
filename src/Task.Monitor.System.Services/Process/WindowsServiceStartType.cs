namespace Task.Monitor.System.Services.Process;

// How a Windows service is configured to start, mirroring the Services snap-in's Startup Type
// column. Whether Automatic is delayed is a separate flag (WindowsServiceInfo.DelayedAutoStart)
// rather than a value here, since it is a modifier on AutomaticStart, not a distinct type.
public enum WindowsServiceStartType
{
    BootStart,
    SystemStart,
    AutomaticStart,
    ManualStart,
    Disabled
}
