namespace Task.Monitor.Cli.Utils;

public static class TerminalCapabilities
{
    // Set to one of the values of ColourMode (Auto | Indexed | Truecolour).
    public const string ColourModeEnvVar = "TASKMON_COLOUR_MODE";

    private static readonly string[] ModernTermPrograms =
    [
        "ghostty",
        "iTerm.app",
        "WezTerm",
        "WarpTerminal",
        "vscode",
        "Apple_Terminal",
    ];

    private static readonly string[] ModernTerminalMarkers =
    [
        "GHOSTTY_RESOURCES_DIR",
        "GHOSTTY_BIN_DIR",
        "KITTY_WINDOW_ID",
        "WT_SESSION",          // Windows Terminal.
        "WEZTERM_PANE",
        "ALACRITTY_WINDOW_ID",
        "ITERM_SESSION_ID",
        "TERM_SESSION_ID",
        "WARP_TERMINAL_SESSION_UUID",
    ];

    private static readonly string[] ModernTermNames =
    [
        "xterm-ghostty",
        "xterm-kitty",
        "alacritty",
        "wezterm",
    ];

    private static readonly string[] ModernTermPrefixes =
    [
        "xterm-256color",
        "screen-256color",
        "tmux-256color",
    ];

    public static bool IsColorModeOverriddenByEnvironment(Func<string, string?> getEnvironmentVariable) =>
        TryParseColourMode(getEnvironmentVariable(ColourModeEnvVar), out _);

    public static bool IsModernTerminal(Func<string, string?> getEnvironmentVariable)
    {
        string? termProgram = getEnvironmentVariable("TERM_PROGRAM");

        if (!string.IsNullOrEmpty(termProgram)) {
            if (ModernTermPrograms.Any(p => string.Equals(termProgram, p, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }
        }

        if (ModernTerminalMarkers.Any(m => !string.IsNullOrEmpty(getEnvironmentVariable(m)))) {
            return true;
        }
        
        string? term = getEnvironmentVariable("TERM");

        if (!string.IsNullOrEmpty(term)) {
            if (ModernTermNames.Any(n => string.Equals(term, n, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }

            if (ModernTermPrefixes.Any(p => term.StartsWith(p, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }
        }

        return false;
    }

    public static ColourMode ResolveEffective(ColourMode configuredMode, Func<string, string?> getEnvironmentVariable)
    {
        if (TryParseColourMode(getEnvironmentVariable(ColourModeEnvVar), out ColourMode envMode)) {
            if (envMode != ColourMode.Auto) {
                configuredMode = envMode;
            }
        }

        if (configuredMode == ColourMode.Auto) {
            return IsModernTerminal(getEnvironmentVariable) ? ColourMode.Indexed : ColourMode.Truecolour;
        }

        return configuredMode;
    }

    // Resolves whether the 16 standard palette colours should be emitted as
    // 4-bit indexed codes.
    public static bool ResolvePreferIndexed(ColourMode configuredMode, Func<string, string?> getEnvironmentVariable) =>
        ResolveEffective(configuredMode, getEnvironmentVariable) == ColourMode.Indexed;

    public static bool TryParseColourMode(string? value, out ColourMode mode)
    {
        mode = ColourMode.Auto;

        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out mode);
    }
}