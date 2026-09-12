namespace Task.Monitor.System.Services.Startup;

// Machine entries run for every user (HKLM, the all-users Startup folder); User entries run for
// the current user only (HKCU, the per-user Startup folder).
public enum StartupEntryScope
{
    Machine,
    User
}
