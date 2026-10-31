namespace Task.Monitor.System.Services.WindowsServices;

internal static partial class WindowsServiceLookup
{
    internal static Dictionary<int, WindowsServiceInfo> BuildServiceMap(IEnumerable<(int ProcessId, WindowsServiceInfo Service)> services)
    {
        Dictionary<int, WindowsServiceInfo> map = new();

        foreach ((int processId, WindowsServiceInfo service) in services) {
            if (processId == 0 || !HasProcess(service.Status)) {
                continue;
            }

            map[processId] = service;
        }

        return map;
    }

    private static bool HasProcess(WindowsServiceStatus status) => status
        is WindowsServiceStatus.Running
        or WindowsServiceStatus.StartPending
        or WindowsServiceStatus.StopPending
        or WindowsServiceStatus.PausePending
        or WindowsServiceStatus.Paused;
}
