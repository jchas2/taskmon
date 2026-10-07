#if __APPLE__
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class LaunchdJobDefinitionTests
{
    private static Dictionary<string, object?> Job(params (string Key, object? Value)[] entries) =>
        entries.ToDictionary(entry => entry.Key, entry => entry.Value);

    private static List<object?> Args(params string[] values) => values.Cast<object?>().ToList();

    [Fact]
    public void Parses_ProgramArguments_With_The_First_As_The_Executable()
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(Job(
            ("Label", "com.example.agent"),
            ("ProgramArguments", Args("/usr/local/bin/agent", "--start", "-v")),
            ("RunAtLoad", true)));

        Assert.NotNull(definition);
        Assert.Equal("com.example.agent", definition.Label);
        Assert.Equal("/usr/local/bin/agent", definition.ExecutablePath);
        Assert.Equal("--start -v", definition.Arguments);
        Assert.Equal("\"/usr/local/bin/agent\" --start -v", definition.Command);
        Assert.True(definition.StartsAtLoad);
    }

    [Fact]
    public void Prefers_Program_And_Treats_ProgramArguments_Zero_As_Argv0()
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(Job(
            ("Program", "/usr/local/bin/agent"),
            ("ProgramArguments", Args("agent-name", "--start"))));

        Assert.NotNull(definition);
        Assert.Equal("/usr/local/bin/agent", definition.ExecutablePath);
        Assert.Equal("--start", definition.Arguments);
    }

    [Fact]
    public void Uses_The_Bare_Path_As_The_Command_When_There_Are_No_Arguments()
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(Job(
            ("Program", "/Applications/App.app/Contents/MacOS/daemon")));

        Assert.NotNull(definition);
        Assert.Null(definition.Arguments);
        Assert.Equal("/Applications/App.app/Contents/MacOS/daemon", definition.Command);
    }

    [Fact]
    public void Quotes_Arguments_Containing_Whitespace()
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(Job(
            ("ProgramArguments", Args("/bin/tool", "--config", "/Library/Application Support/Tool/config.json", ""))));

        Assert.NotNull(definition);
        Assert.Equal("--config \"/Library/Application Support/Tool/config.json\" \"\"", definition.Arguments);
    }

    [Fact]
    public void Resolves_BundleProgram_Against_The_Bundle_Root()
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(
            Job(("Label", "com.example.launcher"), ("BundleProgram", "Contents/Resources/Launcher")),
            "/Applications/Example.app");

        Assert.NotNull(definition);
        Assert.Equal("/Applications/Example.app/Contents/Resources/Launcher", definition.ExecutablePath);
    }

    [Fact]
    public void Ignores_BundleProgram_Without_A_Bundle_Root()
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(
            Job(("BundleProgram", "Contents/Resources/Launcher")));

        Assert.NotNull(definition);
        Assert.Null(definition.ExecutablePath);
        Assert.Equal(string.Empty, definition.Command);
    }

    [Fact]
    public void Starts_At_Load_When_Kept_Alive()
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(Job(("KeepAlive", true)));

        Assert.NotNull(definition);
        Assert.True(definition.StartsAtLoad);
    }

    [Fact]
    public void Starts_At_Load_When_Kept_Alive_On_SuccessfulExit()
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(Job(
            ("KeepAlive", new Dictionary<string, object?> { ["SuccessfulExit"] = false })));

        Assert.NotNull(definition);
        Assert.True(definition.StartsAtLoad);
    }

    [Fact]
    public void Does_Not_Start_At_Load_When_Only_Conditionally_Kept_Alive()
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(Job(
            ("KeepAlive", new Dictionary<string, object?> { ["NetworkState"] = true })));

        Assert.NotNull(definition);
        Assert.False(definition.StartsAtLoad);
    }

    [Fact]
    public void Does_Not_Start_At_Load_For_An_On_Demand_Job()
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(Job(
            ("Label", "com.example.helper"),
            ("MachServices", new Dictionary<string, object?> { ["com.example.helper.xpc"] = true }),
            ("StartInterval", 3600L)));

        Assert.NotNull(definition);
        Assert.False(definition.StartsAtLoad);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Reads_The_Disabled_Key(bool disabled, bool expected)
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(Job(("Disabled", disabled)));

        Assert.NotNull(definition);
        Assert.Equal(expected, definition.Disabled);
    }

    [Fact]
    public void Leaves_Disabled_Unset_When_Absent()
    {
        LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(Job(("Label", "com.example.agent")));

        Assert.NotNull(definition);
        Assert.Null(definition.Disabled);
        Assert.Null(definition.ExecutablePath);
    }

    [Fact]
    public void Returns_Null_For_A_Property_List_That_Is_Not_A_Dictionary()
    {
        Assert.Null(LaunchdJobDefinition.Parse(Args("not", "a", "job")));
        Assert.Null(LaunchdJobDefinition.Parse(null));
    }
}
#endif
