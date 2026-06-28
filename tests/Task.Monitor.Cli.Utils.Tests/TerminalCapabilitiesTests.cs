namespace Task.Monitor.Cli.Utils.Tests;

public sealed class TerminalCapabilitiesTests
{
    private static Func<string, string?> Env(params (string Key, string Value)[] entries)
    {
        Dictionary<string, string?> map = new(StringComparer.Ordinal);

        foreach (var (key, value) in entries) {
            map[key] = value;
        }

        return key => map.GetValueOrDefault(key);
    }

    [Theory]
    [InlineData("ghostty")]
    [InlineData("Ghostty")]
    [InlineData("iTerm.app")]
    [InlineData("WezTerm")]
    [InlineData("vscode")]
    [InlineData("Apple_Terminal")]
    public void IsModernTerminal_Detects_Known_TermProgram(string termProgram)
    {
        Assert.True(TerminalCapabilities.IsModernTerminal(Env(("TERM_PROGRAM", termProgram))));
    }

    [Theory]
    [InlineData("GHOSTTY_RESOURCES_DIR")]
    [InlineData("KITTY_WINDOW_ID")]
    [InlineData("WT_SESSION")]
    [InlineData("WEZTERM_PANE")]
    [InlineData("ALACRITTY_WINDOW_ID")]
    [InlineData("ITERM_SESSION_ID")]
    public void IsModernTerminal_Detects_Terminal_Specific_Markers(string marker)
    {
        Assert.True(TerminalCapabilities.IsModernTerminal(Env((marker, "anything"))));
    }

    [Theory]
    [InlineData("xterm-ghostty")]
    [InlineData("xterm-kitty")]
    [InlineData("alacritty")]
    [InlineData("xterm-256color")]
    [InlineData("tmux-256color")]
    public void IsModernTerminal_Detects_Known_Term(string term)
    {
        Assert.True(TerminalCapabilities.IsModernTerminal(Env(("TERM", term))));
    }

    [Theory]
    [InlineData("xterm")]
    [InlineData("dumb")]
    [InlineData("")]
    public void IsModernTerminal_Rejects_Unknown_Terminals(string term)
    {
        Assert.False(TerminalCapabilities.IsModernTerminal(Env(("TERM", term))));
    }

    [Fact]
    public void IsModernTerminal_Rejects_Empty_Environment()
    {
        Assert.False(TerminalCapabilities.IsModernTerminal(Env()));
    }

    [Fact]
    public void ResolvePreferIndexed_Honours_Explicit_Indexed_Config()
    {
        // Even on an unrecognised terminal, explicit Indexed wins.
        Assert.True(TerminalCapabilities.ResolvePreferIndexed(ColourMode.Indexed, Env(("TERM", "xterm"))));
    }

    [Fact]
    public void ResolvePreferIndexed_Honours_Explicit_Truecolor_Config()
    {
        // Even on a modern terminal, explicit Truecolour wins.
        Assert.False(TerminalCapabilities.ResolvePreferIndexed(ColourMode.Truecolour, Env(("TERM_PROGRAM", "ghostty"))));
    }

    [Fact]
    public void ResolvePreferIndexed_Auto_Detects_Modern_Terminal()
    {
        Assert.True(TerminalCapabilities.ResolvePreferIndexed(ColourMode.Auto, Env(("TERM_PROGRAM", "ghostty"))));
    }

    [Fact]
    public void ResolvePreferIndexed_Auto_Falls_Back_To_Truecolour_On_Unknown()
    {
        Assert.False(TerminalCapabilities.ResolvePreferIndexed(ColourMode.Auto, Env(("TERM", "xterm"))));
    }

    [Theory]
    [InlineData("indexed", true)]
    [InlineData("Indexed", true)]
    [InlineData("truecolour", false)]
    public void ResolvePreferIndexed_Env_Var_Overrides_Config(string envValue, bool expected)
    {
        // Config says the opposite of the env var; env var must win.
        ColourMode config = expected ? ColourMode.Truecolour : ColourMode.Indexed;

        bool result = TerminalCapabilities.ResolvePreferIndexed(
            config,
            Env((TerminalCapabilities.ColourModeEnvVar, envValue)));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolvePreferIndexed_Env_Var_Auto_Falls_Through_To_Detection()
    {
        bool result = TerminalCapabilities.ResolvePreferIndexed(
            ColourMode.Truecolour,
            Env((TerminalCapabilities.ColourModeEnvVar, "auto"), ("TERM_PROGRAM", "ghostty")));

        Assert.True(result);
    }

    [Theory]
    [InlineData("auto", ColourMode.Auto)]
    [InlineData("INDEXED", ColourMode.Indexed)]
    [InlineData("  truecolour  ", ColourMode.Truecolour)]
    public void TryParseColorMode_Parses_Valid_Values(string value, ColourMode expected)
    {
        Assert.True(TerminalCapabilities.TryParseColourMode(value, out ColourMode mode));
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    public void TryParseColorMode_Rejects_Invalid_Values(string? value)
    {
        Assert.False(TerminalCapabilities.TryParseColourMode(value, out _));
    }

    [Fact]
    public void ResolveEffective_Collapses_Auto_To_Indexed_On_Modern_Terminal()
    {
        Assert.Equal(
            ColourMode.Indexed,
            TerminalCapabilities.ResolveEffective(ColourMode.Auto, Env(("TERM_PROGRAM", "ghostty"))));
    }

    [Fact]
    public void ResolveEffective_Collapses_Auto_To_Truecolour_On_Unknown_Terminal()
    {
        Assert.Equal(
            ColourMode.Truecolour,
            TerminalCapabilities.ResolveEffective(ColourMode.Auto, Env(("TERM", "xterm"))));
    }

    [Fact]
    public void ResolveEffective_Never_Returns_Auto()
    {
        ColourMode effective = TerminalCapabilities.ResolveEffective(ColourMode.Auto, Env());
        Assert.NotEqual(ColourMode.Auto, effective);
    }

    [Fact]
    public void ResolveEffective_Env_Var_Overrides_Persisted_Preference()
    {
        // Persisted preference is Truecolour; env var forces Indexed and must win.
        ColourMode effective = TerminalCapabilities.ResolveEffective(
            ColourMode.Truecolour,
            Env((TerminalCapabilities.ColourModeEnvVar, "indexed")));

        Assert.Equal(ColourMode.Indexed, effective);
    }

    [Fact]
    public void IsColorModeOverriddenByEnvironment_True_When_Env_Var_Valid()
    {
        Assert.True(TerminalCapabilities.IsColorModeOverriddenByEnvironment(
            Env((TerminalCapabilities.ColourModeEnvVar, "truecolour"))));
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("")]
    public void IsColorModeOverriddenByEnvironment_False_When_Env_Var_Absent_Or_Invalid(string envValue)
    {
        Assert.False(TerminalCapabilities.IsColorModeOverriddenByEnvironment(
            Env((TerminalCapabilities.ColourModeEnvVar, envValue))));
    }
}
