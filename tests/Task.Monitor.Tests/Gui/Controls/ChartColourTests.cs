using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.Gui.Controls.Summary2;
using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.System.Tests.Controls;

namespace Task.Monitor.Tests.Gui.Controls;

// Every control that hosts a Chart must paint it with the theme's chart.background,
// chart.borderforeground and chart.borderbackground rather than the control colours. Each test
// sets all three to colours distinct from the control pair, so a chart still picking up a control
// colour fails.
public sealed class ChartColourTests
{
    private static readonly Color ChartBackground = ConsolePalette.DarkMagenta;
    private static readonly Color ChartBorderForeground = ConsolePalette.DarkRed;
    private static readonly Color ChartBorderBackground = ConsolePalette.DarkBlue;

    private readonly RunContext runContext;

    public ChartColourTests()
    {
        runContext = new RunContextHelper().GetRunContext();
        runContext.AppConfig.Theme.ChartBackground = ChartBackground;
        runContext.AppConfig.Theme.ChartBorderForeground = ChartBorderForeground;
        runContext.AppConfig.Theme.ChartBorderBackground = ChartBorderBackground;

        Assert.NotEqual(runContext.AppConfig.Theme.Background, runContext.AppConfig.Theme.ChartBackground);
        Assert.NotEqual(runContext.AppConfig.Theme.Background, runContext.AppConfig.Theme.ChartBorderBackground);
        Assert.NotEqual(runContext.AppConfig.Theme.Foreground, runContext.AppConfig.Theme.ChartBorderForeground);
    }

    private ISystemTerminal Terminal => new ForwardingTerminal(runContext.Terminal);

    public static TheoryData<string> PerformancePanels => new() {
        nameof(CpuPerformanceControl),
        nameof(DiskPerformanceControl),
        nameof(GpuPerformanceControl),
        nameof(MemoryPerformanceControl),
        nameof(NetworkPerformanceControl)
    };

    [Theory]
    [MemberData(nameof(PerformancePanels))]
    public void Performance_Panel_Charts_Use_The_Chart_Colours(string panelName)
    {
        Control panel = panelName switch {
            nameof(CpuPerformanceControl) => new CpuPerformanceControl(Terminal, runContext.AppConfig),
            nameof(DiskPerformanceControl) => new DiskPerformanceControl(Terminal, runContext.AppConfig),
            nameof(GpuPerformanceControl) => new GpuPerformanceControl(Terminal, runContext.AppConfig),
            nameof(MemoryPerformanceControl) => new MemoryPerformanceControl(Terminal, runContext.AppConfig),
            nameof(NetworkPerformanceControl) => new NetworkPerformanceControl(Terminal, runContext.AppConfig),
            _ => throw new ArgumentOutOfRangeException(nameof(panelName))
        };

        AssertChartsUseChartColours(Loaded(panel));
    }

    [Fact]
    public void PerformanceControl_Charts_Use_The_Chart_Colours() =>
        AssertChartsUseChartColours(Loaded(
            new PerformanceControl(runContext.ServiceController, Terminal, runContext.AppConfig)));

    [Fact]
    public void SummaryControl2_Charts_Use_The_Chart_Colours() =>
        AssertChartsUseChartColours(Loaded(
            new SummaryControl2(runContext.ServiceController, Terminal, runContext.AppConfig)));

    [Fact]
    public void ThermalsControl_Charts_Use_The_Chart_Colours()
    {
        ThermalsControl control = Loaded(
            new ThermalsControl(runContext.ServiceController, Terminal, runContext.AppConfig));

        // Thermals builds a chart per sensor, so there are none until a snapshot arrives.
        control.Sample(new SystemSnapshot {
            Thermal = new ThermalInfo {
                Metrics = new ThermalMetrics {
                    Sensors = [
                        new ThermalSensor {
                            Component = ThermalComponent.Cpu,
                            ComponentId = "cpu0",
                            SensorName = "Package",
                            Celsius = 55,
                            Source = ThermalSource.NvApi
                        }
                    ]
                }
            }
        });

        AssertChartsUseChartColours(control);
    }

    private static T Loaded<T>(T control) where T : Control
    {
        control.Width = 160;
        control.Height = 48;
        control.Load();
        control.Resize();
        return control;
    }

    private static void AssertChartsUseChartColours(Control host)
    {
        List<Chart> charts = ControlTreeHelper.FindAll<Chart>(host);

        Assert.NotEmpty(charts);
        Assert.All(charts, chart => {
            Assert.Equal(ChartBackground, chart.BackgroundColour);
            Assert.Equal(ChartBorderForeground, chart.BorderForegroundColour);
            Assert.Equal(ChartBorderBackground, chart.BorderBackgroundColour);
        });

        host.Unload();
    }
}
