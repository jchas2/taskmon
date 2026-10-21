using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Drivers;

#pragma warning disable CA1416 // Validate platform compatibility

internal static partial class DriverLookup
{
#if __WIN32__
    internal static DriverInfo[] GetDrivers() =>
        ServiceControlManager.EnumerateServices(WinService.SERVICE_DRIVER, nameof(GetDrivers), (hSCM, entry) => {
            DriverInfo driver = new() {
                Name        = entry.ServiceName,
                DisplayName = entry.DisplayName,
                Status      = MapStatus(entry.Status)
            };

            ServiceControlManager.QueryService(hSCM, entry.ServiceName, hService => PopulateDriverConfig(hService, driver));

            return driver;
        });

    internal static DriverStartType MapStartType(WindowsServiceStartType startType) => startType switch {
        WindowsServiceStartType.BootStart      => DriverStartType.BootStart,
        WindowsServiceStartType.SystemStart    => DriverStartType.SystemStart,
        WindowsServiceStartType.AutomaticStart => DriverStartType.AutomaticStart,
        WindowsServiceStartType.ManualStart    => DriverStartType.ManualStart,
        WindowsServiceStartType.Disabled       => DriverStartType.Disabled,
                                             _ => DriverStartType.ManualStart
    };

    internal static DriverStatus MapStatus(WindowsServiceStatus status) => status switch {
        WindowsServiceStatus.Stopped         => DriverStatus.Stopped,
        WindowsServiceStatus.StartPending    => DriverStatus.StartPending,
        WindowsServiceStatus.StopPending     => DriverStatus.StopPending,
        WindowsServiceStatus.Running         => DriverStatus.Running,
        WindowsServiceStatus.ContinuePending => DriverStatus.ContinuePending,
        WindowsServiceStatus.PausePending    => DriverStatus.PausePending,
        WindowsServiceStatus.Paused          => DriverStatus.Paused,
                                           _ => DriverStatus.Stopped
    };

    private static void PopulateDriverConfig(nint hService, DriverInfo driver)
    {
        if (ServiceControlManager.QueryConfig(hService, driver.Name) is { } config) {
            driver.StartType = MapStartType(config.StartType);
            driver.ImagePath = config.BinaryPathName;
        }

        if (driver.StartType == DriverStartType.AutomaticStart) {
            driver.DelayedAutoStart = ServiceControlManager.QueryDelayedAutoStart(hService);
        }

        if (ImagePathResolver.Expand(driver.ImagePath) is { } expandedPath) {
            driver.Version = WinVer.GetFileVersion(expandedPath);
        }
    }
#endif
}
#pragma warning restore CA1416 // Validate platform compatibility
