using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Cpu;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public static class SummaryPaneTheme
{
    public static void Apply(Control control, AppConfig appConfig)
    {
        CommonControl.ThemeDefaults(control, appConfig.Theme);

        if (control is Chart chart) {
            CommonControl.Theme((Chart)control, appConfig.Theme);
            chart.MetreStyle     = appConfig.MetreStyle;
            chart.ShowGrid       = appConfig.ShowLargeMetreGrid;
            chart.ShowYAxisScale = appConfig.ShowYAxisScale;
        }
        else if (control is EmptyPaneControl or CpuCoresControl) {
            control.BorderColour = appConfig.Theme.ChartBorderForeground;
        }
    }
}
