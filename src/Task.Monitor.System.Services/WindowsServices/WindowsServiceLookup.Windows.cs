using System.Runtime.InteropServices;
using Microsoft.Win32;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.WindowsServices;

#pragma warning disable CA1416 // Validate platform compatibility

// Which Windows service, if any, each process id belongs to: a map ProcessService consults for every
// process sample, rebuilt periodically and whenever a mapped process has gone.
internal static class WindowsServiceLookup
{
#if __WIN32__
    private static Dictionary<int, WindowsServiceInfo> serviceMap = new();
    
    private static readonly Lock criticalSection = new();

    private static int   cyclesSinceRefresh;
    private static bool refreshRequested;
    private static bool attempted;

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

    private static unsafe int GetServiceProcessId(nint hService)
    {
        int pid          = 0;
        nint pss         = Marshal.AllocHGlobal(Marshal.SizeOf<WinService.SERVICE_STATUS_PROCESS>());
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

    internal static int MappedServiceCount
    {
        get {
            lock (criticalSection) {
                return serviceMap.Count;
            }
        }
    }

    internal static int RebuildCount { get; private set; }

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

    private static Dictionary<int, WindowsServiceInfo>? TryBuildServiceMap()
    {
        nint hSCM = WinService.OpenSCManager(null!, null!, WinService.SC_MANAGER_CONNECT);

        if (hSCM == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(WinService.SC_MANAGER_CONNECT));
            return null;
        }

        WindowsServiceInfo[] services = WindowsServiceEnumerator.GetServices();

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
#endif
}

#pragma warning restore CA1416 // Validate platform compatibility
