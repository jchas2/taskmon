namespace Task.Monitor.System.Services.InstalledApps;

// Machine entries are registered under HKLM (installed for every user); User entries are
// registered under HKCU (installed for the current user only).
public enum InstalledAppScope
{
    Machine,
    User
}
