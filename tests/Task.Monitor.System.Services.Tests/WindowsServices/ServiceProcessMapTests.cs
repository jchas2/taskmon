using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Tests.WindowsServices;

public sealed class ServiceProcessMapTests
{
    [Fact]
    public void MapProcesses_Is_Empty_For_No_Services()
    {
        Assert.Empty(WindowsServicesService.MapProcesses([]));
    }

    [Fact]
    public void MapProcesses_Keeps_The_Last_Service_Listed_For_A_Shared_Process()
    {
        Dictionary<int, WindowsServiceInfo> map = WindowsServicesService.MapProcesses([
            Service("Dhcp", WindowsServiceStatus.Running, 100),
            Service("Dnscache", WindowsServiceStatus.Running, 100),
            Service("EventLog", WindowsServiceStatus.Running, 200)
        ]);

        Assert.Equal(2, map.Count);
        Assert.Equal("Dnscache", map[100].ServiceName);
        Assert.Equal("EventLog", map[200].ServiceName);
    }

    [Theory]
    [InlineData(WindowsServiceStatus.Running)]
    [InlineData(WindowsServiceStatus.StartPending)]
    [InlineData(WindowsServiceStatus.StopPending)]
    [InlineData(WindowsServiceStatus.PausePending)]
    [InlineData(WindowsServiceStatus.Paused)]
    public void MapProcesses_Maps_A_Service_In_A_State_With_A_Process(WindowsServiceStatus status)
    {
        Dictionary<int, WindowsServiceInfo> map = WindowsServicesService.MapProcesses([Service("Svc", status, 100)]);

        Assert.Equal("Svc", Assert.Single(map).Value.ServiceName);
    }

    [Theory]
    [InlineData(WindowsServiceStatus.Stopped)]
    [InlineData(WindowsServiceStatus.ContinuePending)]
    public void MapProcesses_Skips_A_Service_In_A_State_Without_A_Process(WindowsServiceStatus status)
    {
        Assert.Empty(WindowsServicesService.MapProcesses([Service("Svc", status, 100)]));
    }

    [Fact]
    public void MapProcesses_Skips_A_Service_Without_A_Process_Id()
    {
        Assert.Empty(WindowsServicesService.MapProcesses([Service("Svc", WindowsServiceStatus.Running, 0)]));
    }

    private static WindowsServiceInfo Service(string name, WindowsServiceStatus status, int processId) =>
        new() { ServiceName = name, DisplayName = name, Status = status, ProcessId = processId };
}
