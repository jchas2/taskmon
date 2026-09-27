using System.Runtime.InteropServices;
using Microsoft.Win32;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Process;

#pragma warning disable CA1416 // Validate platform compatibility

internal static class WindowsServiceLookup
{
#if __WIN32__
    private static Dictionary<int, WindowsServiceInfo> serviceMap = new();
    private static readonly Lock criticalSection = new();

    private static int cyclesSinceRefresh;
    private static bool refreshRequested;
    private static bool attempted;

    internal static int RebuildCount { get; private set; }

    internal static int MappedServiceCount
    {
        get {
            lock (criticalSection) {
                return serviceMap.Count;
            }
        }
    }

    internal static void RefreshIfDue()
    {
        lock (criticalSection) {
            if (!attempted) {
                return;
            }

            if (!WindowsServiceRefreshPolicy.ShouldRefresh(ref cyclesSinceRefresh, refreshRequested)) {
                return;
            }

            refreshRequested = false;
            RebuildServiceMap();
        }
    }

    internal static void RequestRefreshIfStale(ICollection<int> livePids)
    {
        lock (criticalSection) {
            if (!attempted || refreshRequested) {
                return;
            }

            refreshRequested = WindowsServiceRefreshPolicy.HasStalePid(serviceMap.Keys, livePids);
        }
    }

    public static bool GetService(int pid, out WindowsServiceInfo? service)
    {
        lock (criticalSection) {
            if (!attempted) {
                RebuildServiceMap();
            }

            return serviceMap.TryGetValue(pid, out service);
        }
    }

    public static string? GetServiceImagePath(string serviceName)
    {
        const string RegPath = @"SYSTEM\CurrentControlSet\Services\";
        string subKey = $"{RegPath}{serviceName}";
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(subKey);

        if (key == null) {
            TraceEx.WriteLineOnce(subKey, "Failed to open registry key");
        }

        return key?.GetValue("ImagePath")?.ToString() ?? null;
    }

    private static void RebuildServiceMap()
    {
        attempted = true;
        Dictionary<int, WindowsServiceInfo>? rebuilt = TryBuildServiceMap();

        if (rebuilt == null) {
            return;
        }

        serviceMap = rebuilt;
        RebuildCount++;
    }

    private static Dictionary<int, WindowsServiceInfo>? TryBuildServiceMap()
    {
        nint hSCM = WinService.OpenSCManager(null!, null!, WinService.SC_MANAGER_CONNECT);

        if (hSCM == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(WinService.SC_MANAGER_CONNECT));
            return null;
        }

        WindowsServiceInfo[] services = GetServices();

        if (services.Length == 0) {
            WinService.CloseServiceHandle(hSCM);
            return null;
        }

        Dictionary<int, WindowsServiceInfo> map = new();

        for (int i = 0; i < services.Length; i++) {
            WindowsServiceInfo service = services[i];

            nint hService = WinService.OpenService(
                hSCM,
                service.ServiceName,
                WinService.SERVICE_QUERY_STATUS);

            if (hService == nint.Zero) {
                PInvokeErrorHelpers.TraceOnceOnLastError($"{nameof(WinService.OpenService)} {service.ServiceName}");
                continue;
            }

            int pid = GetServiceProcessId(hService);
            WinService.CloseServiceHandle(hService);

            if (pid == 0) {
                continue;
            }

            map[pid] = service;
        }

        WinService.CloseServiceHandle(hSCM);
        return map;
    }

    private static unsafe int GetServiceProcessId(nint hService)
    {
        int pid = 0;
        nint pss = Marshal.AllocHGlobal(Marshal.SizeOf<WinService.SERVICE_STATUS_PROCESS>());
        uint bytesNeeded = 0;

        if (!WinService.QueryServiceStatusEx(
            hService,
            WinService.SC_ENUM_PROCESS_INFO,
            pss,
            (uint)Marshal.SizeOf<WinService.SERVICE_STATUS_PROCESS>(),
            &bytesNeeded)) {

            PInvokeErrorHelpers.TraceOnceOnLastError($"{nameof(WinService.QueryServiceStatusEx)} {hService}");
            Marshal.FreeHGlobal(pss);
            return 0;
        }

        WinService.SERVICE_STATUS_PROCESS ssp = Marshal.PtrToStructure<WinService.SERVICE_STATUS_PROCESS>(pss);

        if (ssp.dwCurrentState == (int)WinService.ServiceCurrentState.SERVICE_RUNNING ||
            ssp.dwCurrentState == (int)WinService.ServiceCurrentState.SERVICE_PAUSE_PENDING ||
            ssp.dwCurrentState == (int)WinService.ServiceCurrentState.SERVICE_PAUSED ||
            ssp.dwCurrentState == (int)WinService.ServiceCurrentState.SERVICE_START_PENDING ||
            ssp.dwCurrentState == (int)WinService.ServiceCurrentState.SERVICE_STOP_PENDING) {

            pid = ssp.dwProcessId;
        }

        Marshal.FreeHGlobal(pss);
        return pid;
    }

    internal static unsafe WindowsServiceInfo[] GetServices()
    {
        WindowsServiceInfo[] services = [];

        nint hSCM = WinService.OpenSCManager(
            null!, null!, WinService.SC_MANAGER_ENUMERATE_SERVICE | WinService.SC_MANAGER_CONNECT);

        if (hSCM == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(WinService.OpenSCManager),
                $"Failed GetServices {nameof(WinService.SC_MANAGER_ENUMERATE_SERVICE)}");

            return services;
        }

        uint bytesNeeded = 0;
        uint servicesReturned = 0;
        uint resumeHandle = 0;

        WinService.EnumServicesStatusEx(
            hSCM,
            WinService.INFO_LEVEL_STANDARD,
            WinService.SERVICE_TYPE_ALL,
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
                $"{nameof(WinService.EnumServicesStatusEx)} {nameof(bytesNeeded)} returned 0");

            WinService.CloseServiceHandle(hSCM);
            return services;
        }

        nint buffer = Marshal.AllocHGlobal((nint)bytesNeeded);
        resumeHandle = 0;

        bool result = WinService.EnumServicesStatusEx(
            hSCM,
            WinService.INFO_LEVEL_STANDARD,
            WinService.SERVICE_TYPE_ALL,
            WinService.SERVICE_STATE_ALL,
            buffer,
            bytesNeeded,
            &bytesNeeded,
            &servicesReturned,
            &resumeHandle,
            null!);

        if (!result) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(WinService.EnumServicesStatusEx),
                $"{nameof(WinService.EnumServicesStatusEx)} failed to allocate {nameof(buffer)}");

            Marshal.FreeHGlobal(buffer);
            WinService.CloseServiceHandle(hSCM);
            return services;
        }

        nint currentPtr = buffer;
        int structSize = Marshal.SizeOf<WinService.ENUM_SERVICE_STATUS_PROCESS>();
        services = new WindowsServiceInfo[servicesReturned];

        for (int i = 0; i < servicesReturned; i++) {
            WinService.ENUM_SERVICE_STATUS_PROCESS status =
                Marshal.PtrToStructure<WinService.ENUM_SERVICE_STATUS_PROCESS>(currentPtr);

            WindowsServiceInfo service = new() {
                ServiceName = status.lpServiceName,
                DisplayName = status.lpDisplayName,
                Status = WindowsServiceConfigMapper.MapStatus((WinService.ServiceCurrentState)status.dwCurrentState)
            };

            PopulateServiceConfig(hSCM, service);
            services[i] = service;
            currentPtr = nint.Add(currentPtr, structSize);
        }

        Marshal.FreeHGlobal(buffer);
        WinService.CloseServiceHandle(hSCM);
        return services;
    }

    private static void PopulateServiceConfig(nint hSCM, WindowsServiceInfo service)
    {
        nint hService = WinService.OpenService(hSCM, service.ServiceName, WinService.SERVICE_QUERY_CONFIG);

        if (hService == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                $"{nameof(WinService.OpenService)}_{service.ServiceName}_Config");
            return;
        }

        try {
            PopulateStartTypeAndLogOnAs(hService, service);
            PopulateDescription(hService, service);
            PopulateDelayedAutoStart(hService, service);
        }
        finally {
            WinService.CloseServiceHandle(hService);
        }
    }

    private static void PopulateStartTypeAndLogOnAs(nint hService, WindowsServiceInfo service)
    {
        WinService.QueryServiceConfig(
            hService, 
            nint.Zero, 
            0, 
            out uint bytesNeeded);

        if (bytesNeeded == 0) {
            return;
        }

        nint buffer = Marshal.AllocHGlobal((int)bytesNeeded);

        try {
            if (!WinService.QueryServiceConfig(
                hService, 
                buffer, 
                bytesNeeded, 
                out _)) {

                PInvokeErrorHelpers.TraceOnceOnLastError(
                    $"{nameof(WinService.QueryServiceConfig)}_{service.ServiceName}");
                return;
            }

            WinService.QUERY_SERVICE_CONFIGW config =
                Marshal.PtrToStructure<WinService.QUERY_SERVICE_CONFIGW>(buffer);

            service.StartType = WindowsServiceConfigMapper.MapStartType((WinService.ServiceStartType)config.dwStartType);
            service.LogOnAs   = string.IsNullOrEmpty(config.lpServiceStartName) 
                ? null 
                : config.lpServiceStartName;
        }
        finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void PopulateDescription(nint hService, WindowsServiceInfo service)
    {
        WinService.QueryServiceConfig2(
            hService, 
            WinService.SERVICE_CONFIG_DESCRIPTION, 
            nint.Zero, 
            0, 
            out uint bytesNeeded);

        if (bytesNeeded == 0) {
            return;
        }

        nint buffer = Marshal.AllocHGlobal((int)bytesNeeded);

        try {
            if (!WinService.QueryServiceConfig2(
                hService, 
                WinService.SERVICE_CONFIG_DESCRIPTION, 
                buffer, 
                bytesNeeded, 
                out _)) {
                
                return;
            }

            WinService.SERVICE_DESCRIPTIONW description =
                Marshal.PtrToStructure<WinService.SERVICE_DESCRIPTIONW>(buffer);

            service.Description = string.IsNullOrEmpty(description.lpDescription) 
                ? null 
                : description.lpDescription;
        }
        finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void PopulateDelayedAutoStart(nint hService, WindowsServiceInfo service)
    {
        if (service.StartType != WindowsServiceStartType.AutomaticStart) {
            return;
        }

        int size = Marshal.SizeOf<WinService.SERVICE_DELAYED_AUTO_START_INFO>();
        nint buffer = Marshal.AllocHGlobal(size);

        try {
            if (!WinService.QueryServiceConfig2(
                hService, 
                WinService.SERVICE_CONFIG_DELAYED_AUTO_START_INFO, 
                buffer, 
                (uint)size, 
                out _)) {
                
                return;
            }

            WinService.SERVICE_DELAYED_AUTO_START_INFO info =
                Marshal.PtrToStructure<WinService.SERVICE_DELAYED_AUTO_START_INFO>(buffer);

            service.DelayedAutoStart = info.fDelayedAutostart;
        }
        finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static bool IsService(int pid) => GetService(pid, out WindowsServiceInfo? _);
#endif
}

#pragma warning restore CA1416 // Validate platform compatibility
