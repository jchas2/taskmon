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
}
#endif
