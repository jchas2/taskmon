using Task.Monitor.System.Configuration;

namespace Task.Monitor.Configuration;

public sealed class Theme
{
    private ConfigSection? themeSection;

    public Theme() { }

    public Theme(ConfigSection configSection) => themeSection = configSection;

    public string Name => themeSection?.Name ?? string.Empty;

    public void Update(ConfigSection configSection) => themeSection = configSection;

    public ConsoleColor Background
    {
        get => themeSection?.GetColour(Constants.Keys.Background, ConsoleColor.Black) ?? ConsoleColor.Black;
        set => themeSection?.Add(Constants.Keys.Background, value.ToString());
    }

    public ConsoleColor BackgroundHighlight
    {
        get => themeSection?.GetColour(Constants.Keys.BackgroundHighlight, ConsoleColor.Cyan) ?? ConsoleColor.Cyan;
        set => themeSection?.Add(Constants.Keys.BackgroundHighlight, value.ToString());
    }

    public ConsoleColor ColumnCommandNormalUserSpace
    {
        get => themeSection?.GetColour(Constants.Keys.ColCmdNormalUserSpace, ConsoleColor.Green) ?? ConsoleColor.Green;
        set => themeSection?.Add(Constants.Keys.ColCmdNormalUserSpace, value.ToString());
    }

    public ConsoleColor ColumnCommandLowPriority
    {
        get => themeSection?.GetColour(Constants.Keys.ColCmdLowPriority, ConsoleColor.Blue) ?? ConsoleColor.Blue;
        set => themeSection?.Add(Constants.Keys.ColCmdLowPriority, value.ToString());
    }

    public ConsoleColor ColumnCommandHighCpu
    {
        get => themeSection?.GetColour(Constants.Keys.ColCmdHighCpu, ConsoleColor.Red) ?? ConsoleColor.Red;
        set => themeSection?.Add(Constants.Keys.ColCmdHighCpu, value.ToString());
    }

    public ConsoleColor ColumnCommandIoBound
    {
        get => themeSection?.GetColour(Constants.Keys.ColCmdIoBound, ConsoleColor.Cyan) ?? ConsoleColor.Cyan;
        set => themeSection?.Add(Constants.Keys.ColCmdIoBound, value.ToString());
    }

    public ConsoleColor ColumnCommandScript
    {
        get => themeSection?.GetColour(Constants.Keys.ColCmdScript, ConsoleColor.Yellow) ?? ConsoleColor.Yellow;
        set => themeSection?.Add(Constants.Keys.ColCmdScript, value.ToString());
    }

    public ConsoleColor ColumnUserCurrentNonRoot
    {
        get => themeSection?.GetColour(Constants.Keys.ColUserCurrentNonRoot, ConsoleColor.Green) ?? ConsoleColor.Green;
        set => themeSection?.Add(Constants.Keys.ColUserCurrentNonRoot, value.ToString());
    }

    public ConsoleColor ColumnUserOtherNonRoot
    {
        get => themeSection?.GetColour(Constants.Keys.ColUserOtherNonRoot, ConsoleColor.Magenta) ?? ConsoleColor.Magenta;
        set => themeSection?.Add(Constants.Keys.ColUserOtherNonRoot, value.ToString());
    }

    public ConsoleColor ColumnUserSystem
    {
        get => themeSection?.GetColour(Constants.Keys.ColUserSystem, ConsoleColor.Gray) ?? ConsoleColor.Gray;
        set => themeSection?.Add(Constants.Keys.ColUserSystem, value.ToString());
    }

    public ConsoleColor ColumnUserRoot
    {
        get => themeSection?.GetColour(Constants.Keys.ColUserRoot, ConsoleColor.White) ?? ConsoleColor.White;
        set => themeSection?.Add(Constants.Keys.ColUserRoot, value.ToString());
    }

    public ConsoleColor CommandBackground
    {
        get => themeSection?.GetColour(Constants.Keys.CommandBackground, ConsoleColor.Cyan) ?? ConsoleColor.Cyan;
        set => themeSection?.Add(Constants.Keys.CommandBackground, value.ToString());
    }

    public ConsoleColor CommandForeground
    {
        get => themeSection?.GetColour(Constants.Keys.CommandForeground, ConsoleColor.Black) ?? ConsoleColor.Black;
        set => themeSection?.Add(Constants.Keys.CommandForeground, value.ToString());
    }

    public ConsoleColor Error
    {
        get => themeSection?.GetColour(Constants.Keys.Error, ConsoleColor.Red) ?? ConsoleColor.Red;
        set => themeSection?.Add(Constants.Keys.Error, value.ToString());
    }

    public ConsoleColor Foreground
    {
        get => themeSection?.GetColour(Constants.Keys.Foreground, ConsoleColor.White) ?? ConsoleColor.White;
        set => themeSection?.Add(Constants.Keys.Foreground, value.ToString());
    }

    public ConsoleColor ForegroundHighlight
    {
        get => themeSection?.GetColour(Constants.Keys.ForegroundHighlight, ConsoleColor.Black) ?? ConsoleColor.Black;
        set => themeSection?.Add(Constants.Keys.ForegroundHighlight, value.ToString());
    }

    public ConsoleColor HeaderBackground
    {
        get => themeSection?.GetColour(Constants.Keys.HeaderBackground, ConsoleColor.DarkGreen) ?? ConsoleColor.DarkGreen;
        set => themeSection?.Add(Constants.Keys.HeaderBackground, value.ToString());
    }

    public ConsoleColor HeaderForeground
    {
        get => themeSection?.GetColour(Constants.Keys.HeaderForeground, ConsoleColor.Black) ?? ConsoleColor.Black;
        set => themeSection?.Add(Constants.Keys.HeaderForeground, value.ToString());
    }

    public ConsoleColor MenubarBackground
    {
        get => themeSection?.GetColour(Constants.Keys.MenubarBackground, ConsoleColor.DarkBlue) ?? ConsoleColor.DarkBlue;
        set => themeSection?.Add(Constants.Keys.MenubarBackground, value.ToString());
    }

    public ConsoleColor MenubarForeground
    {
        get => themeSection?.GetColour(Constants.Keys.MenubarForeground, ConsoleColor.White) ?? ConsoleColor.White;
        set => themeSection?.Add(Constants.Keys.MenubarForeground, value.ToString());
    }

    public ConsoleColor RangeHighBackground
    {
        get => themeSection?.GetColour(Constants.Keys.RangeHighBackground, ConsoleColor.Red) ?? ConsoleColor.Red;
        set => themeSection?.Add(Constants.Keys.RangeHighBackground, value.ToString());
    }

    public ConsoleColor RangeLowBackground
    {
        get => themeSection?.GetColour(Constants.Keys.RangeLowBackground, ConsoleColor.Green) ?? ConsoleColor.Green;
        set => themeSection?.Add(Constants.Keys.RangeLowBackground, value.ToString());
    }

    public ConsoleColor RangeMidBackground
    {
        get => themeSection?.GetColour(Constants.Keys.RangeMidBackground, ConsoleColor.Yellow) ?? ConsoleColor.Yellow;
        set => themeSection?.Add(Constants.Keys.RangeMidBackground, value.ToString());
    }

    public ConsoleColor RangeHighForeground
    {
        get => themeSection?.GetColour(Constants.Keys.RangeHighForeground, ConsoleColor.White) ?? ConsoleColor.White;
        set => themeSection?.Add(Constants.Keys.RangeHighForeground, value.ToString());
    }

    public ConsoleColor RangeLowForeground
    {
        get => themeSection?.GetColour(Constants.Keys.RangeLowForeground, ConsoleColor.White) ?? ConsoleColor.White;
        set => themeSection?.Add(Constants.Keys.RangeLowForeground, value.ToString());
    }

    public ConsoleColor RangeMidForeground
    {
        get => themeSection?.GetColour(Constants.Keys.RangeMidForeground, ConsoleColor.DarkYellow) ?? ConsoleColor.DarkYellow;
        set => themeSection?.Add(Constants.Keys.RangeMidForeground, value.ToString());
    }
}
