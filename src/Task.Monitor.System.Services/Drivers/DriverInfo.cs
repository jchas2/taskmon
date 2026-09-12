using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Drivers;

// A kernel-mode or file-system driver registered with the Service Control Manager - the same
// underlying entity a Windows service is (EnumServicesStatusEx/QueryServiceConfig), just with a
// SERVICE_KERNEL_DRIVER/SERVICE_FILE_SYSTEM_DRIVER type instead of a Win32 one, which is why
// Status/StartType/DelayedAutoStart reuse WindowsServiceStatus/WindowsServiceStartType rather than
// duplicating those enums. Kept as its own type rather than reusing WindowsServiceInfo itself
// since a driver's identity - a binary on disk with a version - is different enough from a
// service's (an account it logs on as, a description) to warrant not overloading one model for
// both.
public sealed class DriverInfo
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public WindowsServiceStatus Status { get; set; }
    public WindowsServiceStartType StartType { get; set; }

    // Only meaningful when StartType is AutomaticStart - see WindowsServiceInfo.DelayedAutoStart.
    public bool DelayedAutoStart { get; set; }

    // The Service Control Manager's raw ImagePath, already expanded from \SystemRoot\... or a
    // bare filename to a real path (see DriverPathResolver) - null when the driver has none
    // (rare, but some pseudo-drivers register with no binary).
    public string? ImagePath { get; set; }

    // Read from the binary's own version resource - null when it has none (common for
    // third-party or very old drivers) or the binary could not be found on disk.
    public string? Version { get; set; }
}
