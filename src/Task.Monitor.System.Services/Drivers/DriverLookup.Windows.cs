using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Drivers;

#pragma warning disable CA1416 // Validate platform compatibility

// The enumeration side of DriversService, split out the same way WindowsServiceLookup is split
// from WindowsServicesService - the Service Control Manager calls stay independently testable in
// spirit (interop-only, no cadence/publish concerns) even though, unlike WindowsServiceLookup,
// there is no pid map to maintain here.
internal static class DriverLookup
{
#if __WIN32__
    internal static unsafe DriverInfo[] GetDrivers()
    {
        DriverInfo[] drivers = [];

        // SC_MANAGER_CONNECT is required to OpenService each driver below for its config; plain
        // enumeration only needs SC_MANAGER_ENUMERATE_SERVICE.
        nint hSCM = WinService.OpenSCManager(
            null!, null!, WinService.SC_MANAGER_ENUMERATE_SERVICE | WinService.SC_MANAGER_CONNECT);

        if (hSCM == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(WinService.OpenSCManager),
                $"Failed GetDrivers {nameof(WinService.SC_MANAGER_ENUMERATE_SERVICE)}");

            return drivers;
        }

        uint bytesNeeded = 0;
        uint driversReturned = 0;
        uint resumeHandle = 0;

        WinService.EnumServicesStatusEx(
            hSCM,
            WinService.INFO_LEVEL_STANDARD,
            WinService.SERVICE_DRIVER,
            WinService.SERVICE_STATE_ALL,
            nint.Zero,
            0,
            &bytesNeeded,
            &driversReturned,
            &resumeHandle,
            null!);

        if (bytesNeeded == 0) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(WinService.EnumServicesStatusEx),
                $"{nameof(WinService.EnumServicesStatusEx)} {nameof(bytesNeeded)} returned 0");

            WinService.CloseServiceHandle(hSCM);
            return drivers;
        }

        nint buffer = Marshal.AllocHGlobal((nint)bytesNeeded);
        resumeHandle = 0;

        bool result = WinService.EnumServicesStatusEx(
            hSCM,
            WinService.INFO_LEVEL_STANDARD,
            WinService.SERVICE_DRIVER,
            WinService.SERVICE_STATE_ALL,
            buffer,
            bytesNeeded,
            &bytesNeeded,
            &driversReturned,
            &resumeHandle,
            null!);

        if (!result) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(WinService.EnumServicesStatusEx),
                $"{nameof(WinService.EnumServicesStatusEx)} failed to allocate {nameof(buffer)}");

            Marshal.FreeHGlobal(buffer);
            WinService.CloseServiceHandle(hSCM);
            return drivers;
        }

        nint currentPtr = buffer;
        int structSize = Marshal.SizeOf<WinService.ENUM_SERVICE_STATUS_PROCESS>();
        drivers = new DriverInfo[driversReturned];

        for (int i = 0; i < driversReturned; i++) {
            WinService.ENUM_SERVICE_STATUS_PROCESS status =
                Marshal.PtrToStructure<WinService.ENUM_SERVICE_STATUS_PROCESS>(currentPtr);

            DriverInfo driver = new() {
                ServiceName = status.lpServiceName,
                DisplayName = status.lpDisplayName,
                Status = WindowsServiceConfigMapper.MapStatus((WinService.ServiceCurrentState)status.dwCurrentState)
            };

            PopulateDriverConfig(hSCM, driver);

            drivers[i] = driver;

            currentPtr = nint.Add(currentPtr, structSize);
        }

        Marshal.FreeHGlobal(buffer);
        WinService.CloseServiceHandle(hSCM);
        return drivers;
    }

    // Fills in StartType, DelayedAutoStart, ImagePath and Version. A driver whose handle cannot be
    // opened (some protected drivers, without elevation) keeps the Name/DisplayName/Status it
    // already has from the enumeration - partial data rather than dropping it from the list.
    private static void PopulateDriverConfig(nint hSCM, DriverInfo driver)
    {
        nint hService = WinService.OpenService(hSCM, driver.ServiceName, WinService.SERVICE_QUERY_CONFIG);

        if (hService == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                $"{nameof(WinService.OpenService)}_{driver.ServiceName}_Config");
            return;
        }

        try {
            PopulateStartTypeAndImagePath(hService, driver);
            PopulateDelayedAutoStart(hService, driver);

            if (DriverPathResolver.Expand(driver.ImagePath) is { } expandedPath) {
                driver.Version = WinVer.GetFileVersion(expandedPath);
            }
        }
        finally {
            WinService.CloseServiceHandle(hService);
        }
    }

    private static void PopulateStartTypeAndImagePath(nint hService, DriverInfo driver)
    {
        WinService.QueryServiceConfig(hService, nint.Zero, 0, out uint bytesNeeded);

        if (bytesNeeded == 0) {
            return;
        }

        nint buffer = Marshal.AllocHGlobal((int)bytesNeeded);

        try {
            if (!WinService.QueryServiceConfig(hService, buffer, bytesNeeded, out _)) {
                PInvokeErrorHelpers.TraceOnceOnLastError(
                    $"{nameof(WinService.QueryServiceConfig)}_{driver.ServiceName}");
                return;
            }

            WinService.QUERY_SERVICE_CONFIGW config =
                Marshal.PtrToStructure<WinService.QUERY_SERVICE_CONFIGW>(buffer);

            driver.StartType = WindowsServiceConfigMapper.MapStartType((WinService.ServiceStartType)config.dwStartType);
            driver.ImagePath = string.IsNullOrEmpty(config.lpBinaryPathName) ? null : config.lpBinaryPathName;
        }
        finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // Only queried for an auto-start driver - the flag is meaningless for Manual/Disabled/Boot/
    // System ones, and virtually no driver sets it, but the check is free and keeps this
    // consistent with WindowsServiceLookup's own handling of the same flag.
    private static void PopulateDelayedAutoStart(nint hService, DriverInfo driver)
    {
        if (driver.StartType != WindowsServiceStartType.AutomaticStart) {
            return;
        }

        int size = Marshal.SizeOf<WinService.SERVICE_DELAYED_AUTO_START_INFO>();
        nint buffer = Marshal.AllocHGlobal(size);

        try {
            if (!WinService.QueryServiceConfig2(
                    hService, WinService.SERVICE_CONFIG_DELAYED_AUTO_START_INFO, buffer, (uint)size, out _)) {
                return;
            }

            WinService.SERVICE_DELAYED_AUTO_START_INFO info =
                Marshal.PtrToStructure<WinService.SERVICE_DELAYED_AUTO_START_INFO>(buffer);

            driver.DelayedAutoStart = info.fDelayedAutostart;
        }
        finally {
            Marshal.FreeHGlobal(buffer);
        }
    }
#endif
}

#pragma warning restore CA1416 // Validate platform compatibility
