#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Tests.WindowsServices;

// Every Windows machine runs the event log service and has the NTFS file system driver, which
// the tests below rely on.
public sealed class ServiceControlManagerTests
{
    [Fact]
    public void EnumerateServices_Lists_Drivers_Separately_From_Services()
    {
        string[] services = ServiceControlManager.EnumerateServices(
            WinService.SERVICE_WIN32, nameof(ServiceControlManagerTests), (_, entry) => entry.ServiceName);

        string[] drivers = ServiceControlManager.EnumerateServices(
            WinService.SERVICE_DRIVER, nameof(ServiceControlManagerTests), (_, entry) => entry.ServiceName);

        Assert.Contains("EventLog", services, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ntfs", services, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Ntfs", drivers, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapStartType_Defaults_Unknown_Values_To_Manual() =>
        Assert.Equal(WindowsServiceStartType.ManualStart, ServiceControlManager.MapStartType((WinService.ServiceStartType)999));

    [Theory]
    [InlineData(WinService.ServiceStartType.SERVICE_BOOT_START, WindowsServiceStartType.BootStart)]
    [InlineData(WinService.ServiceStartType.SERVICE_SYSTEM_START, WindowsServiceStartType.SystemStart)]
    [InlineData(WinService.ServiceStartType.SERVICE_AUTO_START, WindowsServiceStartType.AutomaticStart)]
    [InlineData(WinService.ServiceStartType.SERVICE_DEMAND_START, WindowsServiceStartType.ManualStart)]
    [InlineData(WinService.ServiceStartType.SERVICE_DISABLED, WindowsServiceStartType.Disabled)]
    public void MapStartType_Maps_Every_Known_Value(
        WinService.ServiceStartType raw, WindowsServiceStartType expected) =>
        Assert.Equal(expected, ServiceControlManager.MapStartType(raw));

    [Fact]
    public void MapStatus_Defaults_Unknown_Values_To_Stopped() =>
        Assert.Equal(WindowsServiceStatus.Stopped, ServiceControlManager.MapStatus((WinService.ServiceCurrentState)999));

    [Theory]
    [InlineData(WinService.ServiceCurrentState.SERVICE_STOPPED, WindowsServiceStatus.Stopped)]
    [InlineData(WinService.ServiceCurrentState.SERVICE_START_PENDING, WindowsServiceStatus.StartPending)]
    [InlineData(WinService.ServiceCurrentState.SERVICE_STOP_PENDING, WindowsServiceStatus.StopPending)]
    [InlineData(WinService.ServiceCurrentState.SERVICE_RUNNING, WindowsServiceStatus.Running)]
    [InlineData(WinService.ServiceCurrentState.SERVICE_CONTINUE_PENDING, WindowsServiceStatus.ContinuePending)]
    [InlineData(WinService.ServiceCurrentState.SERVICE_PAUSE_PENDING, WindowsServiceStatus.PausePending)]
    [InlineData(WinService.ServiceCurrentState.SERVICE_PAUSED, WindowsServiceStatus.Paused)]
    public void MapStatus_Maps_Every_Known_Value(
        WinService.ServiceCurrentState raw, WindowsServiceStatus expected) =>
        Assert.Equal(expected, ServiceControlManager.MapStatus(raw));

    [Fact]
    public void QueryConfig_Reads_A_Running_Service()
    {
        ServiceControlManager.ServiceConfig? config = null;
        string? description = null;

        ServiceControlManager.EnumerateServices(
            WinService.SERVICE_WIN32,
            nameof(ServiceControlManagerTests),
            (hSCM, entry) => {
                if (string.Equals(entry.ServiceName, "EventLog", StringComparison.OrdinalIgnoreCase)) {
                    ServiceControlManager.QueryService(hSCM, entry.ServiceName, hService => {
                        config      = ServiceControlManager.QueryConfig(hService, entry.ServiceName);
                        description = ServiceControlManager.QueryDescription(hService);
                    });
                }

                return entry;
            });

        Assert.NotNull(config);
        Assert.Equal(WindowsServiceStartType.AutomaticStart, config.Value.StartType);
        Assert.False(string.IsNullOrWhiteSpace(config.Value.BinaryPathName));
        Assert.False(string.IsNullOrWhiteSpace(description));
    }
}
#endif
