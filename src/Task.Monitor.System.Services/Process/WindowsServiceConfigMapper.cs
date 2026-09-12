using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Process;

// Maps the raw values QueryServiceConfig / EnumServicesStatusEx return into the model-layer enums.
// Kept separate from the interop calls themselves so the mapping is testable without touching the
// Service Control Manager.
public static class WindowsServiceConfigMapper
{
    public static WindowsServiceStartType MapStartType(WinService.ServiceStartType startType) => startType switch {
        WinService.ServiceStartType.SERVICE_BOOT_START => WindowsServiceStartType.BootStart,
        WinService.ServiceStartType.SERVICE_SYSTEM_START => WindowsServiceStartType.SystemStart,
        WinService.ServiceStartType.SERVICE_AUTO_START => WindowsServiceStartType.AutomaticStart,
        WinService.ServiceStartType.SERVICE_DEMAND_START => WindowsServiceStartType.ManualStart,
        WinService.ServiceStartType.SERVICE_DISABLED => WindowsServiceStartType.Disabled,
        _ => WindowsServiceStartType.ManualStart
    };

    public static WindowsServiceStatus MapStatus(WinService.ServiceCurrentState state) => state switch {
        WinService.ServiceCurrentState.SERVICE_STOPPED => WindowsServiceStatus.Stopped,
        WinService.ServiceCurrentState.SERVICE_START_PENDING => WindowsServiceStatus.StartPending,
        WinService.ServiceCurrentState.SERVICE_STOP_PENDING => WindowsServiceStatus.StopPending,
        WinService.ServiceCurrentState.SERVICE_RUNNING => WindowsServiceStatus.Running,
        WinService.ServiceCurrentState.SERVICE_CONTINUE_PENDING => WindowsServiceStatus.ContinuePending,
        WinService.ServiceCurrentState.SERVICE_PAUSE_PENDING => WindowsServiceStatus.PausePending,
        WinService.ServiceCurrentState.SERVICE_PAUSED => WindowsServiceStatus.Paused,
        _ => WindowsServiceStatus.Stopped
    };
}
