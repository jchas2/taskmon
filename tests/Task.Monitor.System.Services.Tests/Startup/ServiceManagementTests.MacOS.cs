#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class ServiceManagementTests
{
    [Fact]
    public void A_Label_That_Does_Not_Exist_Is_Not_Loaded()
    {
        string label = $"com.example.{Guid.NewGuid():N}";

        Assert.False(ServiceManagement.IsLoadedInUserDomain(label));
        Assert.False(ServiceManagement.IsLoadedInSystemDomain(label));
    }

    [Fact]
    public void GetLegacyStatus_Reports_An_Unknown_Plist_As_Not_Registered()
    {
        // SMAppService is macOS 13+, which every supported host is.
        ServiceManagement.LegacyServiceStatus? status = ServiceManagement.GetLegacyStatus(
            Path.Combine("/Library/LaunchDaemons", $"com.example.{Guid.NewGuid():N}.plist"));

        Assert.Equal(ServiceManagement.LegacyServiceStatus.NotRegistered, status);
    }

    // Every Mac loads these system daemons, whether or not anything is using them.
    [Fact]
    public void GetSystemJobs_Lists_The_System_Daemons_With_Their_Programs()
    {
        List<Dictionary<string, object?>>? jobs = ServiceManagement.GetSystemJobs();

        Assert.NotNull(jobs);

        Dictionary<string, object?> syslogd = Assert.Single(jobs, job => job.GetValueOrDefault("Label") as string == "com.apple.syslogd");

        Assert.Equal("/usr/sbin/syslogd", syslogd.GetValueOrDefault("Program") ?? (syslogd.GetValueOrDefault("ProgramArguments") as List<object?>)?[0]);
    }

    // launchd keeps a handful of system daemons running at all times, and reports their pids to any
    // caller, root or not.
    [Fact]
    public void GetSystemJobs_Reports_The_Pid_Of_A_Running_Daemon()
    {
        Assert.Contains(ServiceManagement.GetSystemJobs() ?? [], job => job.GetValueOrDefault("PID") is long pid && pid > 0);
    }

    [Fact]
    public void GetUserJobs_Lists_This_Users_Agents()
    {
        Assert.NotEmpty(ServiceManagement.GetUserJobs() ?? []);
    }
}
#endif
