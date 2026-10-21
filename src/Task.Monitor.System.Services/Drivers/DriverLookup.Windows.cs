using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Drivers;

#pragma warning disable CA1416 // Validate platform compatibility

internal static class DriverLookup
{
#if __WIN32__
    internal static DriverInfo[] GetDrivers() =>
        ServiceControlManager.EnumerateServices(WinService.SERVICE_DRIVER, nameof(GetDrivers), (hSCM, entry) => {
            DriverInfo driver = new() {
                ServiceName = entry.ServiceName,
                DisplayName = entry.DisplayName,
                Status      = entry.Status
            };

            ServiceControlManager.QueryService(hSCM, entry.ServiceName, hService => PopulateDriverConfig(hService, driver));

            return driver;
        });

    private static void PopulateDriverConfig(nint hService, DriverInfo driver)
    {
        if (ServiceControlManager.QueryConfig(hService, driver.ServiceName) is { } config) {
            driver.StartType = config.StartType;
            driver.ImagePath = config.BinaryPathName;
        }

        if (driver.StartType == WindowsServiceStartType.AutomaticStart) {
            driver.DelayedAutoStart = ServiceControlManager.QueryDelayedAutoStart(hService);
        }

        if (ImagePathResolver.Expand(driver.ImagePath) is { } expandedPath) {
            driver.Version = WinVer.GetFileVersion(expandedPath);
        }
    }
#endif
}
#pragma warning restore CA1416 // Validate platform compatibility
