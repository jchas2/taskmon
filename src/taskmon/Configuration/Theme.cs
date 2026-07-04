using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Configuration;

namespace Task.Monitor.Configuration;

public sealed class Theme
{
    private ConfigSection? themeSection;

    public Theme() { }

    public Theme(ConfigSection configSection) => themeSection = configSection;

    public string Name => themeSection?.Name ?? Constants.Sections.ThemeTaskmonDefault;

    public void Update(ConfigSection configSection) => themeSection = configSection;

    private static readonly string[] ColourKeys =
    [
        Constants.Keys.Background,
        Constants.Keys.BackgroundHighlight,
        Constants.Keys.ColCmdNormalUserSpace,
        Constants.Keys.ColCmdLowPriority,
        Constants.Keys.ColCmdHighCpu,
        Constants.Keys.ColCmdIoBound,
        Constants.Keys.ColCmdScript,
        Constants.Keys.ColUserCurrentNonRoot,
        Constants.Keys.ColUserOtherNonRoot,
        Constants.Keys.ColUserSystem,
        Constants.Keys.ColUserRoot,
        Constants.Keys.CommandBackground,
        Constants.Keys.CommandForeground,
        Constants.Keys.DeltaHighlightColour,
        Constants.Keys.Error,
        Constants.Keys.Foreground,
        Constants.Keys.ForegroundHighlight,
        Constants.Keys.HeaderBackground,
        Constants.Keys.HeaderForeground,
        Constants.Keys.MenubarBackground,
        Constants.Keys.MenubarForeground,
        Constants.Keys.RangeHighBackground,
        Constants.Keys.RangeLowBackground,
        Constants.Keys.RangeMidBackground,
        Constants.Keys.RangeHighForeground,
        Constants.Keys.RangeLowForeground,
        Constants.Keys.RangeMidForeground,
    ];

    private Color GetColour(string key, Color fallback) =>
        themeSection?.GetColour(key, fallback) ?? fallback;

    private void SetColour(string key, Color value) =>
        themeSection?.Add(key, ConsolePalette.ToHex(value));

    // Rewrites every colour value to hex, so legacy colour names (e.g. "Black") are persisted as
    // hex on the next save. 
    public void Normalize()
    {
        if (themeSection is null) {
            return;
        }

        foreach (string key in ColourKeys) {
            if (themeSection.Contains(key)) {
                Color colour = ConsolePalette.FromHex(themeSection.GetString(key), ConsolePalette.Black);
                themeSection.Add(key, ConsolePalette.ToHex(colour));
            }
        }
    }

    public ColourMode ColourMode => themeSection?.GetEnum(Constants.Keys.ColourMode, ColourMode.Auto) ?? ColourMode.Auto;

    public Color Background
    {
        get => GetColour(Constants.Keys.Background, ConsolePalette.Black);
        set => SetColour(Constants.Keys.Background, value);
    }

    public Color BackgroundHighlight
    {
        get => GetColour(Constants.Keys.BackgroundHighlight, ConsolePalette.Cyan);
        set => SetColour(Constants.Keys.BackgroundHighlight, value);
    }

    public Color ColumnCommandNormalUserSpace
    {
        get => GetColour(Constants.Keys.ColCmdNormalUserSpace, ConsolePalette.Green);
        set => SetColour(Constants.Keys.ColCmdNormalUserSpace, value);
    }

    public Color ColumnCommandLowPriority
    {
        get => GetColour(Constants.Keys.ColCmdLowPriority, ConsolePalette.Blue);
        set => SetColour(Constants.Keys.ColCmdLowPriority, value);
    }

    public Color ColumnCommandHighCpu
    {
        get => GetColour(Constants.Keys.ColCmdHighCpu, ConsolePalette.Black);
        set => SetColour(Constants.Keys.ColCmdHighCpu, value);
    }

    public Color ColumnCommandIoBound
    {
        get => GetColour(Constants.Keys.ColCmdIoBound, ConsolePalette.Cyan);
        set => SetColour(Constants.Keys.ColCmdIoBound, value);
    }

    public Color ColumnCommandScript
    {
        get => GetColour(Constants.Keys.ColCmdScript, ConsolePalette.Yellow);
        set => SetColour(Constants.Keys.ColCmdScript, value);
    }

    public Color ColumnUserCurrentNonRoot
    {
        get => GetColour(Constants.Keys.ColUserCurrentNonRoot, ConsolePalette.Green);
        set => SetColour(Constants.Keys.ColUserCurrentNonRoot, value);
    }

    public Color ColumnUserOtherNonRoot
    {
        get => GetColour(Constants.Keys.ColUserOtherNonRoot, ConsolePalette.Magenta);
        set => SetColour(Constants.Keys.ColUserOtherNonRoot, value);
    }

    public Color ColumnUserSystem
    {
        get => GetColour(Constants.Keys.ColUserSystem, ConsolePalette.Gray);
        set => SetColour(Constants.Keys.ColUserSystem, value);
    }

    public Color ColumnUserRoot
    {
        get => GetColour(Constants.Keys.ColUserRoot, ConsolePalette.White);
        set => SetColour(Constants.Keys.ColUserRoot, value);
    }

    public Color CommandBackground
    {
        get => GetColour(Constants.Keys.CommandBackground, ConsolePalette.Cyan);
        set => SetColour(Constants.Keys.CommandBackground, value);
    }

    public Color CommandForeground
    {
        get => GetColour(Constants.Keys.CommandForeground, ConsolePalette.Black);
        set => SetColour(Constants.Keys.CommandForeground, value);
    }

    public Color DeltaHighlightColour
    {
        get => GetColour(Constants.Keys.DeltaHighlightColour, ConsolePalette.DarkYellow);
        set => SetColour(Constants.Keys.DeltaHighlightColour, value);
    }

    public Color Error
    {
        get => GetColour(Constants.Keys.Error, ConsolePalette.Red);
        set => SetColour(Constants.Keys.Error, value);
    }

    public Color Foreground
    {
        get => GetColour(Constants.Keys.Foreground, ConsolePalette.White);
        set => SetColour(Constants.Keys.Foreground, value);
    }

    public Color ForegroundHighlight
    {
        get => GetColour(Constants.Keys.ForegroundHighlight, ConsolePalette.Black);
        set => SetColour(Constants.Keys.ForegroundHighlight, value);
    }

    public Color HeaderBackground
    {
        get => GetColour(Constants.Keys.HeaderBackground, ConsolePalette.DarkGreen);
        set => SetColour(Constants.Keys.HeaderBackground, value);
    }

    public Color HeaderForeground
    {
        get => GetColour(Constants.Keys.HeaderForeground, ConsolePalette.Black);
        set => SetColour(Constants.Keys.HeaderForeground, value);
    }

    public Color MenubarBackground
    {
        get => GetColour(Constants.Keys.MenubarBackground, ConsolePalette.DarkBlue);
        set => SetColour(Constants.Keys.MenubarBackground, value);
    }

    public Color MenubarForeground
    {
        get => GetColour(Constants.Keys.MenubarForeground, ConsolePalette.White);
        set => SetColour(Constants.Keys.MenubarForeground, value);
    }

    public Color RangeHighBackground
    {
        get => GetColour(Constants.Keys.RangeHighBackground, ConsolePalette.Red);
        set => SetColour(Constants.Keys.RangeHighBackground, value);
    }

    public Color RangeLowBackground
    {
        get => GetColour(Constants.Keys.RangeLowBackground, ConsolePalette.Green);
        set => SetColour(Constants.Keys.RangeLowBackground, value);
    }

    public Color RangeMidBackground
    {
        get => GetColour(Constants.Keys.RangeMidBackground, ConsolePalette.Yellow);
        set => SetColour(Constants.Keys.RangeMidBackground, value);
    }

    public Color RangeHighForeground
    {
        get => GetColour(Constants.Keys.RangeHighForeground, ConsolePalette.Black);
        set => SetColour(Constants.Keys.RangeHighForeground, value);
    }

    public Color RangeLowForeground
    {
        get => GetColour(Constants.Keys.RangeLowForeground, ConsolePalette.Black);
        set => SetColour(Constants.Keys.RangeLowForeground, value);
    }

    public Color RangeMidForeground
    {
        get => GetColour(Constants.Keys.RangeMidForeground, ConsolePalette.Black);
        set => SetColour(Constants.Keys.RangeMidForeground, value);
    }
}
