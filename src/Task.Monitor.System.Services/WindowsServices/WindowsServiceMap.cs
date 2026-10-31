namespace Task.Monitor.System.Services.WindowsServices;

// Turns one service control manager enumeration into WindowsServiceLookup's pid to service map.
// Kept free of Win32 calls so the filtering can be tested on every platform.
internal static class WindowsServiceMap
{
    public static Dictionary<int, WindowsServiceInfo> Build(IEnumerable<(int ProcessId, WindowsServiceInfo Service)> services)
    {
        Dictionary<int, WindowsServiceInfo> map = new();

        foreach ((int processId, WindowsServiceInfo service) in services) {
            if (processId == 0 || !HasProcess(service.Status)) {
                continue;
            }

            // Services sharing one svchost process all report its pid. The last one enumerated
            // names the process, as it always has.
            map[processId] = service;
        }

        return map;
    }

    // The states in which the service control manager reports a live process id. A stopped
    // service reports 0, and a continue-pending one has never been mapped.
    public static bool HasProcess(WindowsServiceStatus status) => status
        is WindowsServiceStatus.Running
        or WindowsServiceStatus.StartPending
        or WindowsServiceStatus.StopPending
        or WindowsServiceStatus.PausePending
        or WindowsServiceStatus.Paused;
}
