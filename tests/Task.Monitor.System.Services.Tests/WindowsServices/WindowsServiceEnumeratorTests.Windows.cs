#if __WIN32__
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Tests.WindowsServices;

// Every Windows machine runs the event log service and has the NTFS file system driver, which
// the tests below rely on.
public sealed class WindowsServiceEnumeratorTests
{
    [Fact]
    public void GetServices_Leaves_Out_Drivers()
    {
        Assert.DoesNotContain(
            WindowsServiceEnumerator.GetServices(),
            service => string.Equals(service.ServiceName, "Ntfs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetServices_Reads_Each_Services_Configuration()
    {
        WindowsServiceInfo[] services = WindowsServiceEnumerator.GetServices();

        WindowsServiceInfo eventLog = Assert.Single(
            services, service => string.Equals(service.ServiceName, "EventLog", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(WindowsServiceStatus.Running, eventLog.Status);
        Assert.Equal(WindowsServiceStartType.AutomaticStart, eventLog.StartType);
        Assert.False(string.IsNullOrWhiteSpace(eventLog.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(eventLog.Description));
    }

    [Fact]
    public void GetServices_Reports_No_Process_Id_For_A_Stopped_Service()
    {
        Assert.All(
            WindowsServiceEnumerator.GetServices().Where(service => service.Status == WindowsServiceStatus.Stopped),
            service => Assert.Equal(0, service.ProcessId));
    }

    [Fact]
    public void GetServices_Reports_The_Process_And_Image_Of_A_Running_Service()
    {
        WindowsServiceInfo eventLog = Assert.Single(
            WindowsServiceEnumerator.GetServices(),
            service => string.Equals(service.ServiceName, "EventLog", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(WindowsServiceStatus.Running, eventLog.Status);
        Assert.True(eventLog.ProcessId > 0);
        Assert.Contains("svchost", eventLog.ImagePath, StringComparison.OrdinalIgnoreCase);
    }
}
#endif
