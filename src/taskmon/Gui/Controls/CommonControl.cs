using System.Text;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.DriveInputBox;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Controls.TextInputDialog;
using MessageBoxControl = Task.Monitor.System.Controls.MessageBox.MessageBox;
using PickerBoxControl = Task.Monitor.System.Controls.PickerBox.PickerBox;

namespace Task.Monitor.Gui.Controls;

public static class CommonControl
{
    public const string FooterSeparator = "   ";

    public static readonly (string Key, string Label) ClearFilterHotKey = ("Esc", "Clear Filter");
    public static readonly (string Key, string Label) FilterHotKey      = ("f", "Filter");
    public static readonly (string Key, string Label) RefreshHotKey     = ("r", "Refresh");
    public static readonly (string Key, string Label) ScrollHotKey      = ("↑ ↓ PgUp PgDn", "Scroll");
    public static readonly (string Key, string Label) SortHotKey        = ("s", "Sort");

    public static string FooterText(Theme theme, params (string Key, string Label)[] hotKeys)
    {
        if (hotKeys.Length == 0) {
            return string.Empty;
        }

        string keyColour  = ConsolePalette.ForegroundSgr(theme.HotKey);
        string nameColour = ConsolePalette.ForegroundSgr(theme.HotKeyName);

        StringBuilder text = new(ConsolePalette.BackgroundSgr(theme.ListViewBorderBackground));

        for (int i = 0; i < hotKeys.Length; i++) {
            if (i > 0) {
                text.Append(FooterSeparator);
            }

            text.Append(keyColour)
                .Append(FormatHotKey(hotKeys[i].Key))
                .Append(nameColour)
                .Append(' ')
                .Append(hotKeys[i].Label);
        }

        return text.ToString();
    }

    public static string FormatHotKey(string key) =>
        IsFunctionKey(key)
            ? key.ToUpperInvariant()
            : key.ToLowerInvariant();

    private static bool IsFunctionKey(string key) =>
        key.Length is 2 or 3 &&
        key[0] is 'F' or 'f' &&
        int.TryParse(key.AsSpan(1), out int number) &&
        number is >= 1 and <= 24;

    public static void Theme(Chart control, Theme theme)
    {
        control.BackgroundColour       = theme.ChartBackground;
        control.ForegroundColour       = theme.Foreground;
        control.BorderForegroundColour = theme.ChartBorderForeground;
        control.BorderBackgroundColour = theme.ChartBorderBackground;
        control.ColourHigh             = theme.RangeHighBackground;
        control.ColourLow              = theme.RangeLowBackground;
        control.ColourMid              = theme.RangeMidBackground;
        control.YAxisColour            = theme.ChartYAxis;
    }

    public static void Theme(DriveInputBox control, Theme theme)
    {
        control.DialogBackgroundColour                = theme.HeaderBackground;
        control.DialogBorderColour                    = theme.HeaderForeground;
        control.DialogButtonBackgroundColour          = theme.BackgroundHighlight;
        control.DialogButtonForegroundColour          = theme.ForegroundHighlight;
        control.DialogForegroundColour                = theme.HeaderForeground;
        control.ListBackgroundHighlightColour         = theme.BackgroundHighlight;
        control.ListForegroundHighlightColour         = theme.ForegroundHighlight;
        control.ListBackgroundHighlightInactiveColour = theme.BackgroundHighlightInactive;
        control.ListForegroundHighlightInactiveColour = theme.ForegroundHighlightInactive;
        
    }
    
    public static void Theme(ListView control, Theme theme)
    {
        control.BackgroundColour                  = theme.ListViewBackground;
        control.ForegroundColour                  = theme.ListViewForeground;
        control.BorderForegroundColour            = theme.ListViewBorderForeground;
        control.BorderBackgroundColour            = theme.ListViewBorderBackground;
        control.HeaderBackgroundColour            = theme.HeaderBackground;
        control.HeaderForegroundColour            = theme.HeaderForeground;
        control.BackgroundHighlightColour         = theme.BackgroundHighlight;
        control.ForegroundHighlightColour         = theme.ForegroundHighlight;
        control.BackgroundHighlightInactiveColour = theme.BackgroundHighlightInactive;
        control.ForegroundHighlightInactiveColour = theme.ForegroundHighlightInactive;

        foreach (ListViewColumnHeader columnHeader in control.ColumnHeaders) {
            columnHeader.BackgroundColour = theme.HeaderBackground;
            columnHeader.ForegroundColour = theme.HeaderForeground;
        }
    }

    public static void Theme(MessageBoxControl control, Theme theme)
    {
        control.DialogBackgroundColour       = theme.HeaderBackground;
        control.DialogBorderColour           = theme.HeaderForeground;
        control.DialogForegroundColour       = theme.HeaderForeground;
        control.DialogButtonBackgroundColour = theme.BackgroundHighlight;
        control.DialogButtonForegroundColour = theme.ForegroundHighlight;
    }

    public static void Theme(MetreControl control, Theme theme)
    {
        control.BackgroundColour       = theme.MetreBackground;
        control.ForegroundColour       = theme.MetreForeground;
        control.BorderForegroundColour = theme.MetreBorderForeground;
        control.BorderBackgroundColour = theme.MetreBorderBackground;
    }

    public static void Theme(PickerBoxControl control, Theme theme)
    {
        control.DialogBackgroundColour                = theme.HeaderBackground;
        control.DialogBorderColour                    = theme.HeaderForeground;
        control.DialogForegroundColour                = theme.HeaderForeground;
        control.ListBackgroundHighlightColour         = theme.BackgroundHighlight;
        control.ListForegroundHighlightColour         = theme.ForegroundHighlight;
        control.ListBackgroundHighlightInactiveColour = theme.BackgroundHighlightInactive;
        control.ListForegroundHighlightInactiveColour = theme.ForegroundHighlightInactive;
    }

    public static void Theme(TextInputDialog control, Theme theme)
    {
        control.DialogBackgroundColour       = theme.HeaderBackground;
        control.DialogForegroundColour       = theme.HeaderForeground;
        control.DialogButtonBackgroundColour = theme.BackgroundHighlight;
        control.DialogButtonForegroundColour = theme.ForegroundHighlight;
        control.FieldBackgroundColour        = theme.Background;
        control.FieldForegroundColour        = theme.Foreground;
    }

    public static void ThemeDefaults(Control control, Theme theme)
    {
        control.BackgroundColour = theme.Background;
        control.BorderColour     = theme.ControlBorder;
        control.ForegroundColour = theme.Foreground;
    }
}
