#if __WIN32__
using Task.Monitor.Interop.Win32;
#endif

namespace Task.Monitor.System.Services.WindowsServices;

public partial class WindowsServicesService
{
#if __WIN32__
    private const int ERROR_ACCESS_DENIED               = 5;
    private const int ERROR_DEPENDENT_SERVICES_RUNNING  = 1051;
    private const int ERROR_INVALID_SERVICE_CONTROL     = 1052;
    private const int ERROR_SERVICE_REQUEST_TIMEOUT     = 1053;
    private const int ERROR_SERVICE_ALREADY_RUNNING     = 1056;
    private const int ERROR_SERVICE_DISABLED            = 1058;
    private const int ERROR_SERVICE_DOES_NOT_EXIST      = 1060;
    private const int ERROR_SERVICE_CANNOT_ACCEPT_CTRL  = 1061;
    private const int ERROR_SERVICE_NOT_ACTIVE          = 1062;
    private const int ERROR_SERVICE_DEPENDENCY_FAIL     = 1068;
    private const int ERROR_SERVICE_DEPENDENCY_DELETED  = 1075;

    private partial ServiceControlOutcome ApplyControl(string serviceName, bool start)
    {
        int error = start
            ? ServiceControlManager.StartService(serviceName)
            : ServiceControlManager.StopService(serviceName);

        ServiceControlResult result = ToControlResult(error);

        return result == ServiceControlResult.DependentsRunning
            ? new ServiceControlOutcome(result, error, ServiceControlManager.RunningDependents(serviceName))
            : new ServiceControlOutcome(result, error);
    }

    private partial IReadOnlyList<(string ServiceName, WindowsServiceStatus Status)> QueryStatuses() =>
        ServiceControlManager.EnumerateServices(
            WinService.SERVICE_WIN32,
            nameof(QueryStatuses),
            (_, entry) => (entry.ServiceName, entry.Status));

    private partial WindowsServicesSpecs ScanServices()
    {
        WindowsServicesSpecs specs = new();

        specs.Services.AddRange(WindowsServiceEnumerator.GetServices());

        specs.Services.Sort(static (left, right) =>
            string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));

        return specs;
    }

    internal static ServiceControlResult ToControlResult(int error) => error switch {
        0                                 => ServiceControlResult.Succeeded,
        ERROR_SERVICE_ALREADY_RUNNING     => ServiceControlResult.Succeeded,
        ERROR_SERVICE_NOT_ACTIVE          => ServiceControlResult.Succeeded,
        ERROR_ACCESS_DENIED               => ServiceControlResult.AccessDenied,
        ERROR_SERVICE_DISABLED            => ServiceControlResult.Disabled,
        ERROR_DEPENDENT_SERVICES_RUNNING  => ServiceControlResult.DependentsRunning,
        ERROR_SERVICE_DEPENDENCY_FAIL     => ServiceControlResult.DependencyFailed,
        ERROR_SERVICE_DEPENDENCY_DELETED  => ServiceControlResult.DependencyFailed,
        ERROR_INVALID_SERVICE_CONTROL     => ServiceControlResult.CannotStop,
        ERROR_SERVICE_CANNOT_ACCEPT_CTRL  => ServiceControlResult.Busy,
        ERROR_SERVICE_DOES_NOT_EXIST      => ServiceControlResult.NotFound,
        ERROR_SERVICE_REQUEST_TIMEOUT     => ServiceControlResult.TimedOut,
                                        _ => ServiceControlResult.Failed
    };
#endif
}
