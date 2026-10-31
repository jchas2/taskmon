using Microsoft.Win32;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Services.WindowsServices;

#pragma warning disable CA1416 // Validate platform compatibility

internal static class WindowsServiceLookup
{
#if __WIN32__
    private static Dictionary<int, WindowsServiceInfo> serviceMap = new();
    
    private static readonly Lock CriticalSection = new();

    private static int   cyclesSinceRefresh;
    private static bool refreshRequested;
    private static bool attempted;

    public static bool GetService(int pid, out WindowsServiceInfo? service)
    {
        lock (CriticalSection) {
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

    internal static int MappedServiceCount
    {
        get {
            lock (CriticalSection) {
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
        lock (CriticalSection) {
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
        lock (CriticalSection) {
            if (!attempted || refreshRequested) {
                return;
            }

            refreshRequested = WindowsServiceRefreshPolicy.HasStalePid(serviceMap.Keys, livePids);
        }
    }

    private static Dictionary<int, WindowsServiceInfo>? TryBuildServiceMap()
    {
        (int ProcessId, WindowsServiceInfo Service)[] services = WindowsServiceEnumerator.GetServiceProcesses();

        return services.Length == 0
            ? null
            : WindowsServiceMap.Build(services);
    }
#endif
}

#pragma warning restore CA1416 // Validate platform compatibility
