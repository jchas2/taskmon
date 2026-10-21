#if __WIN32__
using Task.Monitor.System.Services.Drivers;
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Tests.Drivers;

public sealed partial class DriverLookupTests
{
    [Theory]
    [InlineData(WindowsServiceStartType.BootStart,      DriverStartType.BootStart)]
    [InlineData(WindowsServiceStartType.SystemStart,    DriverStartType.SystemStart)]
    [InlineData(WindowsServiceStartType.AutomaticStart, DriverStartType.AutomaticStart)]
    [InlineData(WindowsServiceStartType.ManualStart,    DriverStartType.ManualStart)]
    [InlineData(WindowsServiceStartType.Disabled,       DriverStartType.Disabled)]
    public void MapStartType_Maps_Each_Service_Start_Type(WindowsServiceStartType startType, DriverStartType expected)
    {
        Assert.Equal(expected, DriverLookup.MapStartType(startType));
    }

    [Theory]
    [InlineData(WindowsServiceStatus.Stopped,         DriverStatus.Stopped)]
    [InlineData(WindowsServiceStatus.StartPending,    DriverStatus.StartPending)]
    [InlineData(WindowsServiceStatus.StopPending,     DriverStatus.StopPending)]
    [InlineData(WindowsServiceStatus.Running,         DriverStatus.Running)]
    [InlineData(WindowsServiceStatus.ContinuePending, DriverStatus.ContinuePending)]
    [InlineData(WindowsServiceStatus.PausePending,    DriverStatus.PausePending)]
    [InlineData(WindowsServiceStatus.Paused,          DriverStatus.Paused)]
    public void MapStatus_Maps_Each_Service_Status(WindowsServiceStatus status, DriverStatus expected)
    {
        Assert.Equal(expected, DriverLookup.MapStatus(status));
    }
}
#endif
