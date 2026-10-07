#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class ServiceManagementTests
{
    [Fact]
    public void GetLegacyStatus_Reports_An_Unknown_Plist_As_Not_Registered()
    {
        // SMAppService is macOS 13+, which every supported host is.
        ServiceManagement.LegacyServiceStatus? status = ServiceManagement.GetLegacyStatus(
            Path.Combine("/Library/LaunchDaemons", $"com.example.{Guid.NewGuid():N}.plist"));

        Assert.Equal(ServiceManagement.LegacyServiceStatus.NotRegistered, status);
    }

    [Fact]
    public void A_Label_That_Does_Not_Exist_Is_Not_Loaded()
    {
        string label = $"com.example.{Guid.NewGuid():N}";

        Assert.False(ServiceManagement.IsLoadedInUserDomain(label));
        Assert.False(ServiceManagement.IsLoadedInSystemDomain(label));
    }
}
#endif
