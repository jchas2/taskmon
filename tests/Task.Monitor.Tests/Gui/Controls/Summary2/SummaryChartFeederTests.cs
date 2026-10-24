using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Npu;
using Task.Monitor.System.Services.Power;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

public sealed class SummaryChartFeederTests
{
    private readonly RunContext runContext = new RunContextHelper().GetRunContext();

    [Fact]
    public void Labels_The_Npu_Chart_With_Activity_And_Power()
    {
        (SummaryLayoutTree tree, Dictionary<int, Control> panes, Chart chart) = NpuChartPane();
        runContext.AppConfig.ShowMetreNpuNumerically = true;

        SummaryChartFeeder.Feed(tree, panes, SnapshotWithNpu(0.5, watts: 1.3), runContext.AppConfig);

        Assert.Equal($"{0.5:000.0%} · 1.3 W", chart.LabelSeries);
    }

    [Fact]
    public void Leaves_The_Npu_Chart_Alone_Without_An_Npu()
    {
        (SummaryLayoutTree tree, Dictionary<int, Control> panes, Chart chart) = NpuChartPane();

        SummaryChartFeeder.Feed(tree, panes, new SystemSnapshot(), runContext.AppConfig);

        Assert.Equal(string.Empty, chart.LabelSeries);
    }

    private (SummaryLayoutTree Tree, Dictionary<int, Control> Panes, Chart Chart) NpuChartPane()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.FromNodes([new SummaryLayoutNode { Id = 0, ControlType = PaneControlType.Npu }], rootId: 0);
        Chart chart = new(runContext.Terminal) { Width = 40, Height = 8 };

        chart.Resize();
        return (tree, new Dictionary<int, Control> { [0] = chart }, chart);
    }

    [Fact]
    public void Omits_The_Npu_Label_When_The_Setting_Is_Off()
    {
        (SummaryLayoutTree tree, Dictionary<int, Control> panes, Chart chart) = NpuChartPane();
        runContext.AppConfig.ShowMetreNpuNumerically = false;

        SummaryChartFeeder.Feed(tree, panes, SnapshotWithNpu(0.5, watts: 1.3), runContext.AppConfig);

        Assert.Equal(string.Empty, chart.LabelSeries);
    }

    private static SystemSnapshot SnapshotWithNpu(double activeRatio, double watts) =>
        new() {
            Npu   = new NpuInfo { Metrics = new NpuMetrics { Devices = [new NpuDeviceMetrics { Index = 0, ActiveRatio = activeRatio }] } },
            Power = new PowerInfo {
                Metrics = new PowerMetrics {
                    Readings = [new PowerReading { Component = PowerComponent.NeuralEngine, Rail = "Neural Engine", Watts = watts }]
                }
            }
        };
}
