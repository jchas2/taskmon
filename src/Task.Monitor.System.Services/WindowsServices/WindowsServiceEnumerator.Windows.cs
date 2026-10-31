#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.WindowsServices;

internal static class WindowsServiceEnumerator
{
    internal static (int ProcessId, WindowsServiceInfo Service)[] GetServiceProcesses() =>
        ServiceControlManager.EnumerateServices(WinService.SERVICE_WIN32, nameof(GetServiceProcesses), (_, entry) => (
            entry.ProcessId,
            new WindowsServiceInfo {
                ServiceName = entry.ServiceName,
                DisplayName = entry.DisplayName,
                Status      = entry.Status
            }));

    internal static WindowsServiceInfo[] GetServices() =>
        ServiceControlManager.EnumerateServices(WinService.SERVICE_WIN32, nameof(GetServices), (hSCM, entry) => {
            WindowsServiceInfo service = new() {
                ServiceName = entry.ServiceName,
                DisplayName = entry.DisplayName,
                Status      = entry.Status
            };

            ServiceControlManager.QueryService(hSCM, entry.ServiceName, hService => PopulateServiceConfig(hService, service));

            return service;
        });

    private static void PopulateServiceConfig(nint hService, WindowsServiceInfo service)
    {
        if (ServiceControlManager.QueryConfig(hService, service.ServiceName) is { } config) {
            service.StartType = config.StartType;
            service.LogOnAs   = config.StartName;
        }

        service.Description = ServiceControlManager.QueryDescription(hService);

        if (service.StartType == WindowsServiceStartType.AutomaticStart) {
            service.DelayedAutoStart = ServiceControlManager.QueryDelayedAutoStart(hService);
        }
    }
}
#endif
