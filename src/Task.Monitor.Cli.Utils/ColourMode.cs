namespace Task.Monitor.Cli.Utils;

public enum ColourMode
{
    Auto,        // Emit Indexed codes only when a contrast-softening terminal is detected, otherwise Truecolour.
    Indexed,     // Always emit indexed codes for the standard palette colours.
    Truecolour,  // Always emit truecolor codes.
}

