namespace Task.Monitor.System.Services.Startup;

// The enable/disable state Task Manager records under Explorer\StartupApproved. Unknown means no
// such record exists, which the OS treats as enabled.
public enum StartupEntryState
{
    Unknown,
    Enabled,
    Disabled
}
