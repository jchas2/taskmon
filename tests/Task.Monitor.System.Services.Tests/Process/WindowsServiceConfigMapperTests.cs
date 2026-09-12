using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed class WindowsServiceConfigMapperTests
{
    [Theory]
    [InlineData(WinService.ServiceStartType.SERVICE_BOOT_START, WindowsServiceStartType.BootStart)]
    [InlineData(WinService.ServiceStartType.SERVICE_SYSTEM_START, WindowsServiceStartType.SystemStart)]
    [InlineData(WinService.ServiceStartType.SERVICE_AUTO_START, WindowsServiceStartType.AutomaticStart)]
    [InlineData(WinService.ServiceStartType.SERVICE_DEMAND_START, WindowsServiceStartType.ManualStart)]
    [InlineData(WinService.ServiceStartType.SERVICE_DISABLED, WindowsServiceStartType.Disabled)]
    public void MapStartType_Maps_Every_Known_Value(
        WinService.ServiceStartType raw, WindowsServiceStartType expected) =>
        Assert.Equal(expected, WindowsServiceConfigMapper.MapStartType(raw));

    [Fact]
    public void MapStartType_Defaults_Unknown_Values_To_Manual() =>
        Assert.Equal(WindowsServiceStartType.ManualStart, WindowsServiceConfigMapper.MapStartType((WinService.ServiceStartType)999));

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
        Assert.Equal(expected, WindowsServiceConfigMapper.MapStatus(raw));

    [Fact]
    public void MapStatus_Defaults_Unknown_Values_To_Stopped() =>
        Assert.Equal(WindowsServiceStatus.Stopped, WindowsServiceConfigMapper.MapStatus((WinService.ServiceCurrentState)999));
}
