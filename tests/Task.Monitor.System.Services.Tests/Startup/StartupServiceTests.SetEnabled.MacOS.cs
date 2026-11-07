#if __APPLE__
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.LaunchdServices;
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed partial class StartupServiceTests
{
    private const string ProbeLabel = "com.example.taskmon-toggle-probe";

    [Fact]
    public void BuildSetEnabledArguments_Names_The_Subcommand_And_The_Service()
    {
        Assert.Equal(new[] { "disable", "user/501/com.example.job" }, Launchctl.BuildSetEnabledArguments("user/501/com.example.job", enabled: false));
        Assert.Equal(new[] { "enable", "system/com.example.job" },    Launchctl.BuildSetEnabledArguments("system/com.example.job", enabled: true));
    }

    [Fact]
    public void CheckCanSetEnabled_Allows_A_User_Job_Without_Root()
    {
        Assert.Null(StartupService.CheckCanSetEnabled(LaunchdEntry(), enabled: false, isRoot: false, static _ => true));
    }

    [Fact]
    public void CheckCanSetEnabled_Allows_Disabling_A_Job_Background_Items_Blocks()
    {
        Assert.Null(StartupService.CheckCanSetEnabled(LaunchdEntry(blocked: true), enabled: false, isRoot: false, static _ => true));
    }

    [Theory]
    [InlineData(false, StartupToggleResult.AccessDenied)]
    [InlineData(true,  null)]
    public void CheckCanSetEnabled_Needs_Root_For_A_Daemon(bool isRoot, StartupToggleResult? expected)
    {
        StartupEntry entry = LaunchdEntry(StartupEntrySource.LaunchDaemon);

        Assert.Equal(expected, StartupService.CheckCanSetEnabled(entry, enabled: false, isRoot, static _ => true));
    }

    [Fact]
    public void CheckCanSetEnabled_Refuses_An_Entry_With_No_Launchd_Job()
    {
        StartupEntry entry = new() { Name = "Alpha", Source = StartupEntrySource.OpenAtLogin, State = StartupEntryState.Enabled };

        Assert.Equal(StartupToggleResult.NotSupported, StartupService.CheckCanSetEnabled(entry, enabled: false, isRoot: true, static _ => true));
    }

    [Fact]
    public void CheckCanSetEnabled_Reports_A_Missing_Plist_As_Not_Found()
    {
        Assert.Equal(
            StartupToggleResult.NotFound,
            StartupService.CheckCanSetEnabled(LaunchdEntry(), enabled: false, isRoot: true, static _ => false));
    }

    [Fact]
    public void CheckCanSetEnabled_Sends_Enabling_A_Blocked_Job_To_System_Settings()
    {
        Assert.Equal(
            StartupToggleResult.RequiresApproval,
            StartupService.CheckCanSetEnabled(LaunchdEntry(blocked: true), enabled: true, isRoot: true, static _ => true));
    }

    [Theory]
    [InlineData(true,  false, true)]
    [InlineData(false, true,  true)]
    [InlineData(true,  true,  false)]
    [InlineData(null,  true,  false)]
    public void HasOverride_Needs_The_Label_To_Hold_The_Requested_State(bool? disabled, bool enabled, bool expected)
    {
        Dictionary<string, object?> plist = disabled is { } value ? new() { ["com.example.job"] = value } : new();

        Assert.Equal(expected, StartupService.HasOverride(LaunchdOverrides.Parse(plist), "com.example.job", enabled));
    }

    private static StartupEntry LaunchdEntry(
        StartupEntrySource source  = StartupEntrySource.LaunchAgent,
        bool               blocked = false,
        string             origin  = "/Library/LaunchAgents/com.example.job.plist") =>
        new() {
            Name    = "com.example.job",
            Source  = source,
            State   = StartupEntryState.Enabled,
            Origin  = origin,
            Launchd = new LaunchdTarget(
                source == StartupEntrySource.LaunchDaemon ? LaunchdDomain.System : LaunchdDomain.User,
                501,
                "com.example.job",
                blocked)
        };

    [Fact]
    public void SetEnabled_Does_Not_Support_Open_At_Login_And_Leaves_The_Entry_Alone()
    {
        StartupEntry entry = new() { Name = "Alpha", Source = StartupEntrySource.OpenAtLogin, State = StartupEntryState.Enabled };

        Assert.Equal(StartupToggleResult.NotSupported, new StartupService().SetEnabled(entry, enabled: false));
        Assert.Equal(StartupEntryState.Enabled, entry.State);
    }

    // Toggles the override for a made-up label that no plist uses, so nothing loads or stops. launchd
    // keeps the label's override once written, so the label is fixed rather than one per run.
    [Fact]
    public void SetEnabled_Writes_The_Launchd_Override_The_Scan_Reads()
    {
        uint   uid    = TargetUser.Current().Uid;
        string origin = Path.GetTempFileName();

        StartupEntry entry = new() {
            Name    = ProbeLabel,
            Source  = StartupEntrySource.LaunchAgent,
            Scope   = StartupEntryScope.User,
            State   = StartupEntryState.Enabled,
            Origin  = origin,
            Launchd = StartupService.ToLaunchdTarget(StartupEntrySource.LaunchAgent, uid, ProbeLabel, approved: null)
        };

        StartupService service = new();

        try {
            Assert.Equal(StartupToggleResult.Succeeded, service.SetEnabled(entry, enabled: false));
            Assert.Equal(StartupEntryState.Disabled, entry.State);
            Assert.True(LaunchdOverrides.ReadUser(uid).IsDisabled(ProbeLabel, plistDisabled: null));

            Assert.Equal(StartupToggleResult.Succeeded, service.SetEnabled(entry, enabled: true));
            Assert.Equal(StartupEntryState.Enabled, entry.State);
            Assert.False(LaunchdOverrides.ReadUser(uid).IsDisabled(ProbeLabel, plistDisabled: true));
        }
        finally {
            Launchctl.Run(Launchctl.BuildSetEnabledArguments($"user/{uid}/{ProbeLabel}", enabled: true));
            File.Delete(origin);
        }
    }

    [Theory]
    [InlineData(null,  false)]
    [InlineData(true,  false)]
    [InlineData(false, true)]
    public void ToLaunchdTarget_Is_Blocked_Only_When_Background_Items_Disallows_It(bool? approved, bool blocked) =>
        Assert.Equal(blocked, StartupService.ToLaunchdTarget(StartupEntrySource.LaunchAgent, 501, "com.example.job", approved).BlockedByBackgroundItems);

    [Theory]
    [InlineData(StartupEntrySource.LaunchAgent,  "user/501/com.example.job")]
    [InlineData(StartupEntrySource.LoginHelper,  "user/501/com.example.job")]
    [InlineData(StartupEntrySource.LaunchDaemon, "system/com.example.job")]
    public void ToLaunchdTarget_Puts_Daemons_In_The_System_Domain_And_The_Rest_In_The_Users(StartupEntrySource source, string expected) =>
        Assert.Equal(expected, StartupService.ToLaunchdTarget(source, 501, "com.example.job", approved: null).ServiceTarget);

    [Theory]
    [InlineData(0,                StartupToggleResult.Succeeded)]
    [InlineData(1,                StartupToggleResult.AccessDenied)]
    [InlineData(5,                StartupToggleResult.Failed)]
    [InlineData(Launchctl.NotRun, StartupToggleResult.Failed)]
    public void ToToggleResult_Maps_The_Launchctl_Exit_Code(int exitCode, StartupToggleResult expected) =>
        Assert.Equal(expected, StartupService.ToToggleResult(exitCode));
}
#endif
