namespace Task.Monitor.Cli.Utils;

// Controls whether the 16 standard palette colours are emitted as 4-bit ANSI
// indexed codes (which modern terminals contrast-soften) or as
// 24-bit true colour codes (which terminals render verbatim).
public enum ColourMode
{
    Auto,       // Emit Indexed codes only when a contrast-softening terminal is detected, otherwise Truecolour.
    Indexed,    // Always emit indexed codes for the standard palette colours.
    Truecolour,  // Always emit truecolor codes (preserves exact colours, no terminal override).
}

