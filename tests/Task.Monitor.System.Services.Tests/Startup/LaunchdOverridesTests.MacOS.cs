#if __APPLE__
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class LaunchdOverridesTests
{
    private static readonly LaunchdOverrides Overrides = LaunchdOverrides.Parse(new Dictionary<string, object?> {
        ["com.example.disabled"] = true,
        ["com.example.enabled"]  = false,
        ["com.example.junk"]     = "not a bool"
    });

    [Fact]
    public void An_Override_Of_True_Is_Disabled()
    {
        Assert.Equal(StartupEntryState.Disabled, Overrides.ResolveState("com.example.disabled", null));
    }

    [Fact]
    public void An_Override_Of_False_Is_Enabled_Even_When_The_Job_Says_Disabled()
    {
        Assert.Equal(StartupEntryState.Enabled, Overrides.ResolveState("com.example.enabled", plistDisabled: true));
    }

    [Theory]
    [InlineData(true, StartupEntryState.Disabled)]
    [InlineData(false, StartupEntryState.Enabled)]
    public void Falls_Back_To_The_Jobs_Disabled_Key(bool plistDisabled, StartupEntryState expected)
    {
        Assert.Equal(expected, Overrides.ResolveState("com.example.other", plistDisabled));
    }

    [Fact]
    public void Defaults_To_Enabled()
    {
        Assert.Equal(StartupEntryState.Enabled, Overrides.ResolveState("com.example.other", null));
        Assert.Equal(StartupEntryState.Enabled, Overrides.ResolveState(null, null));
    }

    [Fact]
    public void Not_Approved_Is_Disabled_Even_When_Launchd_Would_Run_It()
    {
        Assert.Equal(StartupEntryState.Disabled, Overrides.ResolveState("com.example.enabled", false, approved: false));
    }

    [Fact]
    public void Approved_Still_Needs_Launchd_To_Enable_It()
    {
        Assert.Equal(StartupEntryState.Disabled, Overrides.ResolveState("com.example.disabled", null, approved: true));
        Assert.Equal(StartupEntryState.Enabled, Overrides.ResolveState("com.example.other", null, approved: true));
    }

    [Fact]
    public void Unknown_Approval_Leaves_It_To_Launchd()
    {
        Assert.Equal(StartupEntryState.Enabled, Overrides.ResolveState("com.example.other", null, approved: null));
        Assert.Equal(StartupEntryState.Disabled, Overrides.ResolveState("com.example.other", true, approved: null));
    }

    [Fact]
    public void Contains_Only_Labels_With_A_Boolean_Override()
    {
        Assert.True(Overrides.Contains("com.example.disabled"));
        Assert.True(Overrides.Contains("com.example.enabled"));
        Assert.False(Overrides.Contains("com.example.junk"));
        Assert.False(Overrides.Contains("com.example.other"));
        Assert.False(Overrides.Contains(null));
    }

    [Fact]
    public void An_Unreadable_Database_Is_Empty()
    {
        LaunchdOverrides overrides = LaunchdOverrides.Parse(null);

        Assert.False(overrides.Contains("com.example.disabled"));
        Assert.Equal(StartupEntryState.Enabled, overrides.ResolveState("com.example.disabled", null));
    }
}
#endif
