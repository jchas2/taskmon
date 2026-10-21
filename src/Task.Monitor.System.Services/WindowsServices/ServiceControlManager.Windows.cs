#if __WIN32__
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.WindowsServices;

internal static class ServiceControlManager
{
    public readonly record struct ServiceConfig(
        WindowsServiceStartType StartType,
        string?                 BinaryPathName,
        string?                 StartName);

    public readonly record struct ServiceEntry(
        string               ServiceName,
        string               DisplayName,
        WindowsServiceStatus Status);

    public static unsafe T[] EnumerateServices<T>(uint serviceType, string caller, Func<nint, ServiceEntry, T> map)
    {
        nint hSCM = WinService.OpenSCManager(
            lpMachineName:  null!,
            lpDatabaseName: null!,
            WinService.SC_MANAGER_ENUMERATE_SERVICE | WinService.SC_MANAGER_CONNECT);

        if (hSCM == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(WinService.OpenSCManager),
                $"Failed {caller} {nameof(WinService.SC_MANAGER_ENUMERATE_SERVICE)}");

            return [];
        }

        nint buffer = nint.Zero;

        try {
            uint bytesNeeded      = 0;
            uint servicesReturned = 0;
            uint resumeHandle     = 0;

            WinService.EnumServicesStatusEx(
                hSCM,
                WinService.INFO_LEVEL_STANDARD,
                serviceType,
                WinService.SERVICE_STATE_ALL,
                nint.Zero,
                0,
                &bytesNeeded,
                &servicesReturned,
                &resumeHandle,
                null!);

            if (bytesNeeded == 0) {
                PInvokeErrorHelpers.TraceOnceOnLastError(
                    nameof(WinService.EnumServicesStatusEx),
                    $"{nameof(WinService.EnumServicesStatusEx)} {nameof(bytesNeeded)} returned 0 for {caller}");

                return [];
            }

            buffer       = Marshal.AllocHGlobal((nint)bytesNeeded);
            resumeHandle = 0;

            if (!WinService.EnumServicesStatusEx(
                hSCM,
                WinService.INFO_LEVEL_STANDARD,
                serviceType,
                WinService.SERVICE_STATE_ALL,
                buffer,
                bytesNeeded,
                &bytesNeeded,
                &servicesReturned,
                &resumeHandle,
                null!)) {

                PInvokeErrorHelpers.TraceOnceOnLastError(
                    nameof(WinService.EnumServicesStatusEx),
                    $"{nameof(WinService.EnumServicesStatusEx)} failed to fill the buffer for {caller}");

                return [];
            }

            T[]  results    = new T[servicesReturned];
            nint currentPtr = buffer;
            int  structSize = Marshal.SizeOf<WinService.ENUM_SERVICE_STATUS_PROCESS>();

            for (int i = 0; i < servicesReturned; i++) {
                WinService.ENUM_SERVICE_STATUS_PROCESS status =
                    Marshal.PtrToStructure<WinService.ENUM_SERVICE_STATUS_PROCESS>(currentPtr);

                ServiceEntry entry = new(
                    status.lpServiceName,
                    status.lpDisplayName,
                    MapStatus((WinService.ServiceCurrentState)status.dwCurrentState));

                results[i] = map(hSCM, entry);
                currentPtr = nint.Add(currentPtr, structSize);
            }

            return results;
        }
        finally {
            if (buffer != nint.Zero) {
                Marshal.FreeHGlobal(buffer);
            }

            WinService.CloseServiceHandle(hSCM);
        }
    }

    internal static WindowsServiceStartType MapStartType(WinService.ServiceStartType startType) => startType switch {
        WinService.ServiceStartType.SERVICE_BOOT_START   => WindowsServiceStartType.BootStart,
        WinService.ServiceStartType.SERVICE_SYSTEM_START => WindowsServiceStartType.SystemStart,
        WinService.ServiceStartType.SERVICE_AUTO_START   => WindowsServiceStartType.AutomaticStart,
        WinService.ServiceStartType.SERVICE_DEMAND_START => WindowsServiceStartType.ManualStart,
        WinService.ServiceStartType.SERVICE_DISABLED     => WindowsServiceStartType.Disabled,
                                                       _ => WindowsServiceStartType.ManualStart
    };

    internal static WindowsServiceStatus MapStatus(WinService.ServiceCurrentState state) => state switch {
        WinService.ServiceCurrentState.SERVICE_STOPPED          => WindowsServiceStatus.Stopped,
        WinService.ServiceCurrentState.SERVICE_START_PENDING    => WindowsServiceStatus.StartPending,
        WinService.ServiceCurrentState.SERVICE_STOP_PENDING     => WindowsServiceStatus.StopPending,
        WinService.ServiceCurrentState.SERVICE_RUNNING          => WindowsServiceStatus.Running,
        WinService.ServiceCurrentState.SERVICE_CONTINUE_PENDING => WindowsServiceStatus.ContinuePending,
        WinService.ServiceCurrentState.SERVICE_PAUSE_PENDING    => WindowsServiceStatus.PausePending,
        WinService.ServiceCurrentState.SERVICE_PAUSED           => WindowsServiceStatus.Paused,
                                                              _ => WindowsServiceStatus.Stopped
    };

    public static ServiceConfig? QueryConfig(nint hService, string serviceName)
    {
        WinService.QueryServiceConfig(
            hService,
            nint.Zero,
            0,
            out uint bytesNeeded);

        if (bytesNeeded == 0) {
            return null;
        }

        nint buffer = Marshal.AllocHGlobal((int)bytesNeeded);

        try {
            if (!WinService.QueryServiceConfig(hService, buffer, bytesNeeded, out _)) {
                PInvokeErrorHelpers.TraceOnceOnLastError($"{nameof(WinService.QueryServiceConfig)}_{serviceName}");
                return null;
            }

            WinService.QUERY_SERVICE_CONFIGW config = Marshal.PtrToStructure<WinService.QUERY_SERVICE_CONFIGW>(buffer);

            return new ServiceConfig(
                MapStartType((WinService.ServiceStartType)config.dwStartType),
                string.IsNullOrEmpty(config.lpBinaryPathName) ? null : config.lpBinaryPathName,
                string.IsNullOrEmpty(config.lpServiceStartName) ? null : config.lpServiceStartName);
        }
        finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static bool QueryDelayedAutoStart(nint hService)
    {
        int  size   = Marshal.SizeOf<WinService.SERVICE_DELAYED_AUTO_START_INFO>();
        nint buffer = Marshal.AllocHGlobal(size);

        try {
            if (!WinService.QueryServiceConfig2(
                hService,
                WinService.SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                buffer,
                (uint)size,
                out _)) {

                return false;
            }

            return Marshal.PtrToStructure<WinService.SERVICE_DELAYED_AUTO_START_INFO>(buffer).fDelayedAutostart;
        }
        finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static string? QueryDescription(nint hService)
    {
        WinService.QueryServiceConfig2(
            hService,
            WinService.SERVICE_CONFIG_DESCRIPTION,
            nint.Zero,
            0,
            out uint bytesNeeded);

        if (bytesNeeded == 0) {
            return null;
        }

        nint buffer = Marshal.AllocHGlobal((int)bytesNeeded);

        try {
            if (!WinService.QueryServiceConfig2(
                hService,
                WinService.SERVICE_CONFIG_DESCRIPTION,
                buffer,
                bytesNeeded,
                out _)) {

                return null;
            }

            WinService.SERVICE_DESCRIPTIONW description = Marshal.PtrToStructure<WinService.SERVICE_DESCRIPTIONW>(buffer);

            return string.IsNullOrEmpty(description.lpDescription)
                ? null
                : description.lpDescription;
        }
        finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static bool QueryService(nint hSCM, string serviceName, Action<nint> query)
    {
        nint hService = WinService.OpenService(hSCM, serviceName, WinService.SERVICE_QUERY_CONFIG);

        if (hService == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError($"{nameof(WinService.OpenService)}_{serviceName}_Config");
            return false;
        }

        try {
            query(hService);
            return true;
        }
        finally {
            WinService.CloseServiceHandle(hService);
        }
    }
}
#endif
