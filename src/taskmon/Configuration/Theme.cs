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

    internal static readonly string[] ColourKeys =
    [
        Constants.Keys.Background,
        Constants.Keys.BackgroundHighlight,
        Constants.Keys.BackgroundHighlightInactive,
        Constants.Keys.ChartBackground,
        Constants.Keys.ChartBorderForeground,
        Constants.Keys.ChartBorderBackground,
        Constants.Keys.ChartYAxis,
        Constants.Keys.ChartTitle,
        Constants.Keys.ChartGrid,
        Constants.Keys.MetreBackground,
        Constants.Keys.MetreForeground,
        Constants.Keys.MetreBorderForeground,
        Constants.Keys.MetreBorderBackground,
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
        Constants.Keys.ControlBorder,
        Constants.Keys.DeltaHighlightColour,
        Constants.Keys.Error,
        Constants.Keys.Foreground,
        Constants.Keys.ForegroundHighlight,
        Constants.Keys.ForegroundHighlightInactive,
        Constants.Keys.FocusSelectionColour,
        Constants.Keys.HeaderBackground,
        Constants.Keys.HeaderForeground,
        Constants.Keys.HeatmapSizeSmall,
        Constants.Keys.HeatmapSizeMid,
        Constants.Keys.HeatmapSizeLarge,
        Constants.Keys.HeatmapStateScanning,
        Constants.Keys.HeatmapStateCompleted,
        Constants.Keys.HeatmapStateFaulted,
        Constants.Keys.ListViewBackground,
        Constants.Keys.ListViewForeground,
        Constants.Keys.ListViewBorderForeground,
        Constants.Keys.ListViewBorderBackground,
        Constants.Keys.MenubarBackground,
        Constants.Keys.MenubarForeground,
        Constants.Keys.PerformanceCpuKernel,
        Constants.Keys.PerformanceCpuUser,
        Constants.Keys.PerformanceMemoryInUse,
        Constants.Keys.PerformanceMemoryModified,
        Constants.Keys.PerformanceMemoryStandby,
        Constants.Keys.PerformanceMemoryFree,
        Constants.Keys.PerformancePanelBackground,
        Constants.Keys.PerformancePanelForeground,
        Constants.Keys.PerformancePanelTitlebarBackground,
        Constants.Keys.PerformancePanelTitlebarForeground,
        Constants.Keys.PropertyKey,
        Constants.Keys.PropertyValue,
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

    public Color BackgroundHighlightInactive
    {
        get => GetColour(Constants.Keys.BackgroundHighlightInactive, ConsolePalette.Cyan);
        set => SetColour(Constants.Keys.BackgroundHighlightInactive, value);
    }

    public Color ChartBackground
    {
        get => GetColour(Constants.Keys.ChartBackground, Background);
        set => SetColour(Constants.Keys.ChartBackground, value);
    }

    public Color ChartBorderForeground
    {
        get => GetColour(Constants.Keys.ChartBorderForeground, Foreground);
        set => SetColour(Constants.Keys.ChartBorderForeground, value);
    }

    public Color ChartBorderBackground
    {
        get => GetColour(Constants.Keys.ChartBorderBackground, Background);
        set => SetColour(Constants.Keys.ChartBorderBackground, value);
    }

    public Color MetreBackground
    {
        get => GetColour(Constants.Keys.MetreBackground, Background);
        set => SetColour(Constants.Keys.MetreBackground, value);
    }

    public Color MetreForeground
    {
        get => GetColour(Constants.Keys.MetreForeground, Foreground);
        set => SetColour(Constants.Keys.MetreForeground, value);
    }

    public Color MetreBorderForeground
    {
        get => GetColour(Constants.Keys.MetreBorderForeground, Foreground);
        set => SetColour(Constants.Keys.MetreBorderForeground, value);
    }

    public Color MetreBorderBackground
    {
        get => GetColour(Constants.Keys.MetreBorderBackground, Background);
        set => SetColour(Constants.Keys.MetreBorderBackground, value);
    }

    public Color ChartYAxis
    {
        get => GetColour(Constants.Keys.ChartYAxis, ConsolePalette.White);
        set => SetColour(Constants.Keys.ChartYAxis, value);
    }

    public Color ChartTitle
    {
        get => GetColour(Constants.Keys.ChartTitle, ConsolePalette.White);
        set => SetColour(Constants.Keys.ChartTitle, value);
    }

    public Color ChartGrid
    {
        get => GetColour(Constants.Keys.ChartGrid, ConsolePalette.DarkGray);
        set => SetColour(Constants.Keys.ChartGrid, value);
    }

    public Color ControlBorder
    {
        get => GetColour(Constants.Keys.ControlBorder, ConsolePalette.White);
        set => SetColour(Constants.Keys.ControlBorder, value);
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

    public Color ForegroundHighlightInactive
    {
        get => GetColour(Constants.Keys.ForegroundHighlightInactive, ConsolePalette.Black);
        set => SetColour(Constants.Keys.ForegroundHighlightInactive, value);
    }

    public Color HeatmapSizeSmall
    {
        get => GetColour(Constants.Keys.HeatmapSizeSmall, ConsolePalette.Green);
        set => SetColour(Constants.Keys.HeatmapSizeSmall, value);
    }

    public Color HeatmapSizeMid
    {
        get => GetColour(Constants.Keys.HeatmapSizeMid, ConsolePalette.Yellow);
        set => SetColour(Constants.Keys.HeatmapSizeMid, value);
    }

    public Color HeatmapSizeLarge
    {
        get => GetColour(Constants.Keys.HeatmapSizeLarge, ConsolePalette.Red);
        set => SetColour(Constants.Keys.HeatmapSizeLarge, value);
    }

    public Color HeatmapStateScanning
    {
        get => GetColour(Constants.Keys.HeatmapStateScanning, ConsolePalette.Yellow);
        set => SetColour(Constants.Keys.HeatmapStateScanning, value);
    }

    public Color HeatmapStateCompleted
    {
        get => GetColour(Constants.Keys.HeatmapStateCompleted, ConsolePalette.Green);
        set => SetColour(Constants.Keys.HeatmapStateCompleted, value);
    }

    public Color HeatmapStateFaulted
    {
        get => GetColour(Constants.Keys.HeatmapStateFaulted, ConsolePalette.Red);
        set => SetColour(Constants.Keys.HeatmapStateFaulted, value);
    }

    public Color FocusSelectionColour
    {
        get => GetColour(Constants.Keys.FocusSelectionColour, ConsolePalette.Yellow);
        set => SetColour(Constants.Keys.FocusSelectionColour, value);
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
    
    public Color ListViewBorderForeground
    {
        get => GetColour(Constants.Keys.ListViewBorderForeground, Foreground);
        set => SetColour(Constants.Keys.ListViewBorderForeground, value);
    }

    public Color ListViewBorderBackground
    {
        get => GetColour(Constants.Keys.ListViewBorderBackground, Background);
        set => SetColour(Constants.Keys.ListViewBorderBackground, value);
    }

    public Color ListViewBackground
    {
        get => GetColour(Constants.Keys.ListViewBackground, ConsolePalette.Black);
        set => SetColour(Constants.Keys.ListViewBackground, value);
    }

    public Color ListViewForeground
    {
        get => GetColour(Constants.Keys.ListViewForeground, ConsolePalette.White);
        set => SetColour(Constants.Keys.ListViewForeground, value);
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

    public Color PerformancePanelBackground
    {
        get => GetColour(Constants.Keys.PerformancePanelBackground, ConsolePalette.Black);
        set => SetColour(Constants.Keys.PerformancePanelBackground, value);
    }

    public Color PerformancePanelForeground
    {
        get => GetColour(Constants.Keys.PerformancePanelForeground, ConsolePalette.White);
        set => SetColour(Constants.Keys.PerformancePanelForeground, value);
    }

    public Color PerformancePanelTitlebarBackground
    {
        get => GetColour(Constants.Keys.PerformancePanelTitlebarBackground, ConsolePalette.DarkBlue);
        set => SetColour(Constants.Keys.PerformancePanelTitlebarBackground, value);
    }

    public Color PerformancePanelTitlebarForeground
    {
        get => GetColour(Constants.Keys.PerformancePanelTitlebarForeground, ConsolePalette.White);
        set => SetColour(Constants.Keys.PerformancePanelTitlebarForeground, value);
    }

    public Color PerformanceCpuKernel
    {
        get => GetColour(Constants.Keys.PerformanceCpuKernel, ConsolePalette.Red);
        set => SetColour(Constants.Keys.PerformanceCpuKernel, value);
    }

    public Color PerformanceCpuUser
    {
        get => GetColour(Constants.Keys.PerformanceCpuUser, ConsolePalette.Green);
        set => SetColour(Constants.Keys.PerformanceCpuUser, value);
    }

    public Color PerformanceMemoryInUse
    {
        get => GetColour(Constants.Keys.PerformanceMemoryInUse, ConsolePalette.Red);
        set => SetColour(Constants.Keys.PerformanceMemoryInUse, value);
    }

    public Color PerformanceMemoryModified
    {
        get => GetColour(Constants.Keys.PerformanceMemoryModified, ConsolePalette.Green);
        set => SetColour(Constants.Keys.PerformanceMemoryModified, value);
    }

    public Color PerformanceMemoryStandby
    {
        get => GetColour(Constants.Keys.PerformanceMemoryStandby, ConsolePalette.Yellow);
        set => SetColour(Constants.Keys.PerformanceMemoryStandby, value);
    }

    public Color PerformanceMemoryFree
    {
        get => GetColour(Constants.Keys.PerformanceMemoryFree, ConsolePalette.Green);
        set => SetColour(Constants.Keys.PerformanceMemoryFree, value);
    }

    public Color PropertyKey
    {
        get => GetColour(Constants.Keys.PropertyKey, Foreground);
        set => SetColour(Constants.Keys.PropertyKey, value);
    }

    public Color PropertyValue
    {
        get => GetColour(Constants.Keys.PropertyValue, Foreground);
        set => SetColour(Constants.Keys.PropertyValue, value);
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
