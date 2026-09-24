using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Cpu;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Tests.Controls;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

public sealed class SummaryPaneThemeTests
{
    // None of these is white (the base Control.BorderColour default) or equal to another, so a
    // pane left unthemed, or themed from the wrong key, fails.
    private static readonly Color Background = ColorTranslator.FromHtml("#101010");
    private static readonly Color Foreground = ColorTranslator.FromHtml("#202020");
    private static readonly Color ChartBackground = ColorTranslator.FromHtml("#303030");
    private static readonly Color ChartBorderForeground = ColorTranslator.FromHtml("#404040");
    private static readonly Color ChartBorderBackground = ColorTranslator.FromHtml("#505050");
    private static readonly Color ChartYAxis = ColorTranslator.FromHtml("#606060");

    private readonly RunContext runContext;

    public SummaryPaneThemeTests()
    {
        runContext = new RunContextHelper().GetRunContext();

        ConfigSection themeSection = new("Summary Pane Theme");
        themeSection.Add(Constants.Keys.Background, "#101010");
        themeSection.Add(Constants.Keys.Foreground, "#202020");
        themeSection.Add(Constants.Keys.ChartBackground, "#303030");
        themeSection.Add(Constants.Keys.ChartBorderForeground, "#404040");
        themeSection.Add(Constants.Keys.ChartBorderBackground, "#505050");
        themeSection.Add(Constants.Keys.ChartYAxis, "#606060");
        runContext.AppConfig.Theme.Update(themeSection);
    }

    [Fact]
    public void CpuCores_Pane_Border_Takes_The_Chart_Border_Colour()
    {
        CpuCoresControl control = new(runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        SummaryPaneTheme.Apply(control, runContext.AppConfig);

        Assert.Equal(ChartBorderForeground.ToArgb(), control.BorderColour.ToArgb());
        Assert.Equal(Background.ToArgb(), control.BackgroundColour.ToArgb());
        Assert.Equal(Foreground.ToArgb(), control.ForegroundColour.ToArgb());
    }

    [Fact]
    public void Empty_Pane_Border_Takes_The_Chart_Border_Colour()
    {
        EmptyPaneControl control = new(runContext.Terminal);

        SummaryPaneTheme.Apply(control, runContext.AppConfig);

        Assert.Equal(ChartBorderForeground.ToArgb(), control.BorderColour.ToArgb());
    }

    [Fact]
    public void Chart_Pane_Takes_The_Chart_Colours()
    {
        Chart chart = new(runContext.Terminal);

        SummaryPaneTheme.Apply(chart, runContext.AppConfig);

        Assert.Equal(ChartBackground.ToArgb(), chart.BackgroundColour.ToArgb());
        Assert.Equal(Foreground.ToArgb(), chart.ForegroundColour.ToArgb());
        Assert.Equal(ChartBorderForeground.ToArgb(), chart.BorderForegroundColour.ToArgb());
        Assert.Equal(ChartBorderBackground.ToArgb(), chart.BorderBackgroundColour.ToArgb());
        Assert.Equal(ChartYAxis.ToArgb(), chart.YAxisColour.ToArgb());
    }
}
