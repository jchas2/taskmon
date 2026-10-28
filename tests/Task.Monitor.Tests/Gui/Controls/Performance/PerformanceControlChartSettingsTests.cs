using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls.Performance;

public sealed class PerformanceControlChartSettingsTests
{
    private readonly RunContext runContext = new RunContextHelper().GetRunContext();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Panel_Charts_Follow_ShowSmallMetreGrid_On_Every_Load(bool showGrid)
    {
        PerformanceControl ctrl = new(runContext.ServiceController, runContext.Terminal, runContext.AppConfig) {
            Width  = 120,
            Height = 40
        };

        runContext.AppConfig.ShowSmallMetreGrid = !showGrid;
        runContext.AppConfig.ShowLargeMetreGrid = !showGrid;
        ctrl.Load();
        ctrl.Unload();

        runContext.AppConfig.ShowSmallMetreGrid = showGrid;
        ctrl.Load();

        List<PerformancePanelControl> panels = ctrl.Controls.OfType<PerformancePanelControl>().ToList();

        Assert.NotEmpty(panels);
        Assert.All(panels, panel => Assert.Equal(showGrid, panel.Chart.ShowGrid));

        ctrl.Unload();
    }
}
