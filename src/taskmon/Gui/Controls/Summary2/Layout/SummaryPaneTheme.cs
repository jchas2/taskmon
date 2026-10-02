using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Cpu;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public static class SummaryPaneTheme
{
    public static void Apply(Control control, AppConfig appConfig)
    {
        Theme theme = appConfig.Theme;

        control.BackgroundColour = theme.Background;
        control.ForegroundColour = theme.Foreground;

        if (control is Chart chart) {
            chart.BackgroundColour = theme.ChartBackground;
            chart.BorderForegroundColour = theme.ChartBorderForeground;
            chart.BorderBackgroundColour = theme.ChartBorderBackground;
            chart.ColourHigh = theme.RangeHighBackground;
            chart.ColourLow = theme.RangeLowBackground;
            chart.ColourMid = theme.RangeMidBackground;
            chart.MetreStyle = appConfig.MetreStyle;
            chart.ShowYAxisScale = appConfig.ShowYAxisScale;
            chart.YAxisColour = theme.ChartYAxis;
        }
        else if (control is EmptyPaneControl or CpuCoresControl) {
            control.BorderColour = theme.ChartBorderForeground;
        }
    }
}
