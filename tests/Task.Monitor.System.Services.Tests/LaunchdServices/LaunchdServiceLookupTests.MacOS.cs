#if __APPLE__
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.LaunchdServices;

namespace Task.Monitor.System.Services.Tests.LaunchdServices;

public sealed class LaunchdServiceLookupTests
{
    private const string Folder = "/Library/LaunchDaemons";
    private const string Label  = "com.example.daemon";

    private static readonly LaunchdOverrides NoOverrides = LaunchdOverrides.Empty;

    private static LaunchdServiceLookup.Plist Daemon(
        string label     = Label,
        bool   runAtLoad = false,
        bool   keepAlive = false,
        bool?  disabled  = null)
    {
        Dictionary<string, object?> plist = new() {
            ["Label"]            = label,
            ["ProgramArguments"] = new List<object?> { "/usr/local/libexec/daemon", "--serve" },
            ["RunAtLoad"]        = runAtLoad,
            ["KeepAlive"]        = keepAlive
        };

        if (disabled is { } value) {
            plist["Disabled"] = value;
        }

        return new LaunchdServiceLookup.Plist($"{Folder}/{label}.plist", Folder, LaunchdJobDefinition.Parse(plist));
    }

    [Fact]
    public void GetServices_Lists_syslogd_As_A_System_Daemon()
    {
        LaunchdServiceInfo syslogd = Assert.Single(
            LaunchdServiceLookup.GetServices(new()), service => service.Label == "com.apple.syslogd");

        Assert.Equal(LaunchdDomain.System, syslogd.Domain);
        Assert.Equal("/System/Library/LaunchDaemons", syslogd.Group);
        Assert.Equal("/usr/sbin/syslogd", syslogd.ProgramPath);
        Assert.Equal("Apple Inc.", syslogd.Publisher);
    }

    private static Dictionary<string, object?> Job(string label = Label, long? pid = null, long? lastExitStatus = null)
    {
        Dictionary<string, object?> job = new() {
            ["Label"]            = label,
            ["Program"]          = "/usr/local/libexec/daemon",
            ["ProgramArguments"] = new List<object?> { "/usr/local/libexec/daemon" },
            ["OnDemand"]         = true
        };

        if (pid is { } value) {
            job["PID"] = value;
        }

        if (lastExitStatus is { } status) {
            job["LastExitStatus"] = status;
        }

        return job;
    }

    private static LaunchdServiceInfo MergeOne(
        LaunchdServiceLookup.Plist                           plist,
        IReadOnlyList<IReadOnlyDictionary<string, object?>>? jobs,
        LaunchdOverrides?                                    overrides = null) =>
        Assert.Single(LaunchdServiceLookup.Merge(LaunchdDomain.System, [plist], jobs, overrides ?? NoOverrides));

    // launchd tracks every running app as an "application." job; those aren't services.
    [Fact]
    public void Merge_Leaves_Out_Running_Apps()
    {
        List<LaunchdServiceInfo> services = LaunchdServiceLookup.Merge(
            LaunchdDomain.User, [], [Job("application.com.example.App.1234", pid: 42)], NoOverrides);

        Assert.Empty(services);
    }

    [Fact]
    public void Merge_Lists_A_Job_With_No_Plist_Under_Its_Domain()
    {
        Dictionary<string, object?> job = Job("com.example.registered", pid: 42);
        job["OnDemand"] = false;

        LaunchdServiceInfo service = Assert.Single(LaunchdServiceLookup.Merge(LaunchdDomain.User, [], [job], NoOverrides));

        Assert.Equal(LaunchdServiceLookup.UserDomainGroup, service.Group);
        Assert.Equal(LaunchdServiceStatus.Running, service.Status);
        Assert.Equal(LaunchdStartType.KeepAlive, service.StartType);
        Assert.Equal("/usr/local/libexec/daemon", service.ProgramPath);
        Assert.Null(service.PlistPath);
    }

    [Fact]
    public void Merge_Marks_A_Loaded_Job_Without_A_Pid_As_Waiting()
    {
        LaunchdServiceInfo service = MergeOne(Daemon(), [Job()]);

        Assert.Equal(LaunchdServiceStatus.Waiting, service.Status);
        Assert.Null(service.Pid);
    }

    [Fact]
    public void Merge_Marks_A_Loaded_Job_With_A_Pid_As_Running()
    {
        LaunchdServiceInfo service = MergeOne(Daemon(), [Job(pid: 321, lastExitStatus: 0)]);

        Assert.Equal(LaunchdServiceStatus.Running, service.Status);
        Assert.Equal(321, service.Pid);
        Assert.Equal(0, service.LastExitStatus);
    }

    [Fact]
    public void Merge_Marks_A_Plist_Launchd_Has_Not_Loaded_As_Not_Loaded()
    {
        Assert.Equal(LaunchdServiceStatus.NotLoaded, MergeOne(Daemon(), []).Status);
    }

    [Fact]
    public void Merge_Marks_Every_Status_Unknown_When_The_Job_List_Is_Unreadable()
    {
        Assert.Equal(LaunchdServiceStatus.Unknown, MergeOne(Daemon(), null).Status);
    }

    [Fact]
    public void Merge_Reads_The_Plist_Details()
    {
        LaunchdServiceInfo service = MergeOne(Daemon(), []);

        Assert.Equal(Label, service.Label);
        Assert.Equal(LaunchdDomain.System, service.Domain);
        Assert.Equal(Folder, service.Group);
        Assert.Equal("/usr/local/libexec/daemon", service.ProgramPath);
        Assert.Equal("--serve", service.Arguments);
        Assert.Equal($"{Folder}/{Label}.plist", service.PlistPath);
    }

    [Fact]
    public void Merge_Takes_Disabled_From_The_Overrides_Over_The_Plist()
    {
        LaunchdOverrides overrides = LaunchdOverrides.Parse(new Dictionary<string, object?> { [Label] = true });

        Assert.True(MergeOne(Daemon(disabled: false), [], overrides).Disabled);
        Assert.True(MergeOne(Daemon(disabled: true), []).Disabled);
        Assert.False(MergeOne(Daemon(), []).Disabled);
    }

    [Fact]
    public void Merge_Uses_The_File_Name_When_The_Plist_Has_No_Label()
    {
        LaunchdServiceLookup.Plist plist = new($"{Folder}/com.example.unlabelled.plist", Folder, null);

        LaunchdServiceInfo service = MergeOne(plist, []);

        Assert.Equal("com.example.unlabelled", service.Label);
        Assert.Equal(LaunchdStartType.OnDemand, service.StartType);
    }

    [Theory]
    [InlineData(false, false, LaunchdStartType.OnDemand)]
    [InlineData(true,  false, LaunchdStartType.AtLoad)]
    [InlineData(false, true,  LaunchdStartType.KeepAlive)]
    [InlineData(true,  true,  LaunchdStartType.KeepAlive)]
    public void ToStartType_Maps_RunAtLoad_And_KeepAlive(bool runAtLoad, bool keepAlive, LaunchdStartType expected)
    {
        Assert.Equal(expected, LaunchdServiceLookup.ToStartType(Daemon(runAtLoad: runAtLoad, keepAlive: keepAlive).Definition));
    }
}
#endif
