#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.WindowsServices;

// Every Win32 service with its configuration read - the list the Services view shows, and the
// source WindowsServiceLookup builds its process id map from.
internal static class WindowsServiceEnumerator
{
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
