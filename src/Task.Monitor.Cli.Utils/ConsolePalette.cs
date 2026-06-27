using System.Drawing;
using System.Globalization;

namespace Task.Monitor.Cli.Utils;

public static class ConsolePalette
{
    public static readonly Color Black       = Color.FromArgb(0, 0, 0);
    public static readonly Color DarkBlue    = Color.FromArgb(0, 0, 128);
    public static readonly Color DarkGreen   = Color.FromArgb(0, 128, 0);
    public static readonly Color DarkCyan    = Color.FromArgb(0, 128, 128);
    public static readonly Color DarkRed     = Color.FromArgb(128, 0, 0);
    public static readonly Color DarkMagenta = Color.FromArgb(128, 0, 128);
    public static readonly Color DarkYellow  = Color.FromArgb(128, 128, 0);
    public static readonly Color Gray        = Color.FromArgb(192, 192, 192);
    public static readonly Color DarkGray    = Color.FromArgb(128, 128, 128);
    public static readonly Color Blue        = Color.FromArgb(0, 0, 255);
    public static readonly Color Green       = Color.FromArgb(0, 255, 0);
    public static readonly Color Cyan        = Color.FromArgb(0, 255, 255);
    public static readonly Color Red         = Color.FromArgb(255, 0, 0);
    public static readonly Color Magenta     = Color.FromArgb(255, 0, 255);
    public static readonly Color Yellow      = Color.FromArgb(255, 255, 0);
    public static readonly Color White       = Color.FromArgb(255, 255, 255);

    // Alpha 0 sentinel: render as the terminal default transparent background.
    public static readonly Color Transparent = Color.FromArgb(0, 0, 0, 0);

    private static readonly Dictionary<string, Color> NamedColours = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"]       = Black,
        ["darkblue"]    = DarkBlue,
        ["darkgreen"]   = DarkGreen,
        ["darkcyan"]    = DarkCyan,
        ["darkred"]     = DarkRed,
        ["darkmagenta"] = DarkMagenta,
        ["darkyellow"]  = DarkYellow,
        ["gray"]        = Gray,
        ["grey"]        = Gray,
        ["darkgray"]    = DarkGray,
        ["darkgrey"]    = DarkGray,
        ["blue"]        = Blue,
        ["green"]       = Green,
        ["cyan"]        = Cyan,
        ["red"]         = Red,
        ["magenta"]     = Magenta,
        ["yellow"]      = Yellow,
        ["white"]       = White,
        ["transparent"] = Transparent,
        ["none"]        = Transparent,
    };

    public static bool IsTransparent(this Color colour) => colour.A == 0;

    public static Color FromHex(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) {
            return fallback;
        }

        string text = value.Trim();
        string hex = text.StartsWith('#') ? text[1..] : text;

        if ((hex.Length == 6 || hex.Length == 8) &&
            uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint packed)) {
            if (hex.Length == 6) {
                return Color.FromArgb(
                    255,
                    (int)((packed >> 16) & 0xFF),
                    (int)((packed >> 8)  & 0xFF),
                    (int)(packed & 0xFF));
            }

            return Color.FromArgb(
                (int)((packed >> 24) & 0xFF),
                (int)((packed >> 16) & 0xFF),
                (int)((packed >> 8)  & 0xFF),
                (int)(packed & 0xFF));
        }

        return NamedColours.GetValueOrDefault(text, fallback);
    }

    public static string ToHex(Color colour)
    {
        if (colour.A == 0) {
            return "transparent";
        }

        return colour.A == 255
            ? $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}"
            : $"#{colour.A:X2}{colour.R:X2}{colour.G:X2}{colour.B:X2}";
    }

    public static string BackgroundSgr(Color colour) =>
        colour.A == 0
            ? "[49m"
            : $"[48;2;{colour.R};{colour.G};{colour.B}m";

    public static string ForegroundSgr(Color colour) =>
        colour.A == 0
            ? "[39m"
            : $"[38;2;{colour.R};{colour.G};{colour.B}m";
}
