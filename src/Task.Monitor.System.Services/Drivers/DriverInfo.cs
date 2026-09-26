using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Drivers;

// A kernel-mode or file-system driver registered with the Service Control Manager.
public sealed class DriverInfo
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public WindowsServiceStatus Status { get; set; }
    public WindowsServiceStartType StartType { get; set; }
    public bool DelayedAutoStart { get; set; }
    public string? ImagePath { get; set; }
    public string? Version { get; set; }
}
