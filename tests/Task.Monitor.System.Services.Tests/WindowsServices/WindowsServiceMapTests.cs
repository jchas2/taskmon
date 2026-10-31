using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Tests.WindowsServices;

public sealed class WindowsServiceMapTests
{
    [Fact]
    public void Build_Is_Empty_For_An_Empty_Enumeration()
    {
        Assert.Empty(WindowsServiceMap.Build([]));
    }

    [Fact]
    public void Build_Keeps_The_Last_Service_Enumerated_For_A_Shared_Process()
    {
        Dictionary<int, WindowsServiceInfo> map = WindowsServiceMap.Build([
            (100, Service("Dhcp", WindowsServiceStatus.Running)),
            (100, Service("Dnscache", WindowsServiceStatus.Running)),
            (200, Service("EventLog", WindowsServiceStatus.Running))
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
    public void Build_Maps_A_Service_In_A_State_With_A_Process(WindowsServiceStatus status)
    {
        Dictionary<int, WindowsServiceInfo> map = WindowsServiceMap.Build([(100, Service("Svc", status))]);

        Assert.Equal("Svc", Assert.Single(map).Value.ServiceName);
    }

    [Theory]
    [InlineData(WindowsServiceStatus.Stopped)]
    [InlineData(WindowsServiceStatus.ContinuePending)]
    public void Build_Skips_A_Service_In_A_State_Without_A_Process(WindowsServiceStatus status)
    {
        Assert.Empty(WindowsServiceMap.Build([(100, Service("Svc", status))]));
    }

    [Fact]
    public void Build_Skips_A_Service_Without_A_Process_Id()
    {
        Assert.Empty(WindowsServiceMap.Build([(0, Service("Svc", WindowsServiceStatus.Running))]));
    }

    private static WindowsServiceInfo Service(string name, WindowsServiceStatus status) =>
        new() { ServiceName = name, DisplayName = name, Status = status };
}
