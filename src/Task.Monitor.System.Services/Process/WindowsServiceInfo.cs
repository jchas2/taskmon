namespace Task.Monitor.System.Services.Process;

// A Windows service (a daemon), not one of this project's worker services. Named for the
// distinction because inside Task.Monitor.System.Services the bare word "Service" already means
// the latter.
public sealed class WindowsServiceInfo
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WindowsServiceStatus Status { get; set; }
    public WindowsServiceStartType StartType { get; set; }

    // Only meaningful when StartType is AutomaticStart - distinguishes "Automatic" from
    // "Automatic (Delayed Start)".
    public bool DelayedAutoStart { get; set; }

    // The account the service runs as (e.g. "LocalSystem", "NT AUTHORITY\NetworkService", a named
    // account), exactly as the Service Control Manager stores it. Kept raw here, the same as
    // InstalledAppScope - a display-layer formatter turns well-known accounts into the friendlier
    // names the Services snap-in shows.
    public string? LogOnAs { get; set; }
}
