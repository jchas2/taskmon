using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Cpu;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

// Applies the theme to the Control behind a single pane - shared by SummaryControl2 and
// LayoutDesignerScreen for the same reason as SummaryPaneControlFactory: a pane should look the
// same in both, which two hand-kept copies of this did not manage (CpuCoresControl's border was
// themed in the designer and left at its white default on the running summary).
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
            // Neither has a Border*Colour pair of its own - both draw their border in BorderColour,
            // which is also what the designer's selection highlight and real focus swap.
            // CpuCoresControl reads its own metre colours from AppConfig.
            control.BorderColour = theme.ChartBorderForeground;
        }
    }
}
