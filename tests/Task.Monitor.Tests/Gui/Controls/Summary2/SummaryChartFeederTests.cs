using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Npu;
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

public sealed class SummaryChartFeederTests
{
    private const double Gibibyte = 1024.0 * 1024.0 * 1024.0;
    private const double Mebibyte = 1024.0 * 1024.0;

    private readonly RunContext runContext = new RunContextHelper().GetRunContext();

    private static SystemSnapshot DiskSnapshot(double bytesPerSecond) =>
        new() { Disk = new DiskInfo { Metrics = new DiskMetrics { ReadBytesPerSecond = bytesPerSecond } } };

    // Regression test: the pane was fed MB/s on an axis with no units, so a 3 GB/s peak read "3.3k"
    // above a "2.1 MB/s" label.
    [Fact]
    public void Disk_Chart_Shows_Bytes_On_Its_Axis_And_Label()
    {
        RecordingTerminal terminal = new();
        SummaryPane             pane = new(PaneControlType.Disk);
        LayoutTree<SummaryPane> tree = new(pane);

        Chart chart = Assert.IsType<Chart>(
            SummaryPaneControlFactory.Create(pane, runContext.ServiceController, terminal, runContext.AppConfig));

        chart.Width  = 40;
        chart.Height = 10;
        chart.Resize();

        Dictionary<int, Control> panes = new() { [0] = chart };
        runContext.AppConfig.ShowMetreDiskNumerically = true;

        SummaryChartFeeder.Feed(tree, panes, DiskSnapshot(3.0 * Gibibyte), runContext.AppConfig);
        terminal.Reset();
        SummaryChartFeeder.Feed(tree, panes, DiskSnapshot(2.0 * Mebibyte), runContext.AppConfig);

        Assert.Equal("2.0 MB/s", chart.LabelSeries);
        Assert.Contains("3G", terminal.Output);
        Assert.Contains("Disk 2.0 MB/s", terminal.Output);
    }

    [Fact]
    public void Labels_The_Npu_Chart_With_Activity_And_Power()
    {
        (LayoutTree<SummaryPane> tree, Dictionary<int, Control> panes, Chart chart) = NpuChartPane();
        runContext.AppConfig.ShowMetreNpuNumerically = true;

        SummaryChartFeeder.Feed(tree, panes, SnapshotWithNpu(0.5, watts: 1.3), runContext.AppConfig);

        Assert.Equal($"{0.5:000.0%} · 1.3 W", chart.LabelSeries);
    }

    [Fact]
    public void Leaves_The_Npu_Chart_Alone_Without_An_Npu()
    {
        (LayoutTree<SummaryPane> tree, Dictionary<int, Control> panes, Chart chart) = NpuChartPane();

        SummaryChartFeeder.Feed(tree, panes, new SystemSnapshot(), runContext.AppConfig);

        Assert.Equal(string.Empty, chart.LabelSeries);
    }

    private (LayoutTree<SummaryPane> Tree, Dictionary<int, Control> Panes, Chart Chart) NpuChartPane()
    {
        LayoutTree<SummaryPane> tree = new(new SummaryPane(PaneControlType.Npu));
        Chart chart = new(runContext.Terminal) { Width = 40, Height = 8 };

        chart.Resize();
        return (tree, new Dictionary<int, Control> { [0] = chart }, chart);
    }

    [Fact]
    public void Omits_The_Npu_Label_When_The_Setting_Is_Off()
    {
        (LayoutTree<SummaryPane> tree, Dictionary<int, Control> panes, Chart chart) = NpuChartPane();
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
