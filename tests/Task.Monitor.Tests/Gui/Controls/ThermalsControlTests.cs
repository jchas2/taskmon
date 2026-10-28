using System.Reflection;
using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ThermalsControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public ThermalsControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Charts_Pick_Up_Changed_Grid_And_Y_Axis_Settings_On_The_Next_Load()
    {
        ThermalsControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(Sensor(ThermalComponent.Gpu, "111", "GPU", 40)));

        Chart chart = Assert.Single(ctrl.Charts.Values);
        Assert.Equal(runContext.AppConfig.ShowLargeMetreGrid, chart.ShowGrid);

        ctrl.Unload();
        runContext.AppConfig.ShowLargeMetreGrid = !chart.ShowGrid;
        runContext.AppConfig.ShowYAxisScale     = false;
        ctrl.Load();

        Assert.Equal(runContext.AppConfig.ShowLargeMetreGrid, chart.ShowGrid);
        Assert.False(chart.ShowYAxisScale);

        ctrl.Unload();
    }

    [Fact]
    public void Collapses_A_Multi_Sensor_Drive_To_A_Single_Chart()
    {
        ThermalsControl ctrl = CreateControl();

        // A drive that reports a composite plus a hotter controller sensor is still one "Disk 0".
        ctrl.Sample(SnapshotWith(
            Sensor(ThermalComponent.Disk, "0", "Composite", 35, ThermalSource.NvmeHealthLog),
            Sensor(ThermalComponent.Disk, "0", "Sensor 2", 63, ThermalSource.NvmeHealthLog)));

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();

        // One "Disk 0" chart (one title in a single frame), plotting the composite - not the
        // controller sensor.
        int first = output.IndexOf("Disk 0", StringComparison.Ordinal);
        Assert.True(first >= 0);
        Assert.Equal(-1, output.IndexOf("Disk 0", first + 1, StringComparison.Ordinal));
        Assert.Contains("35°C", output);
        Assert.DoesNotContain("63°C", output);

        ctrl.Unload();
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        ThermalsControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private ThermalsControl CreateChartsOnlyControl()
    {
        ThermalsControl ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width         = 120,
            Height        = 40,
            ShowPowerPane = false
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    private ThermalsControl CreateControl(int width = 120, int height = 40)
    {
        ThermalsControl ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = width,
            Height = height
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    [Fact]
    public void Draws_One_Titled_Chart_Per_Component()
    {
        ThermalsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Sensor(ThermalComponent.Gpu, "111", "GPU", 42),
            Sensor(ThermalComponent.Disk, "0", "Composite", 35, ThermalSource.NvmeHealthLog)));
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("GPU", output);
        Assert.Contains("Disk 0", output);
        Assert.Contains("42°C", output);
        Assert.Contains("35°C", output);

        // Two charts fit; no scroll bar or arrows.
        Assert.DoesNotContain("▲", output);
        Assert.DoesNotContain("▼", output);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Draws_The_Empty_State_When_There_Are_No_Sensors()
    {
        ThermalsControl ctrl = CreateControl();
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("No thermal sensors detected", output);
#if __APPLE__
        Assert.Contains("SMC and HID temperature sensors", output);
#else
        Assert.Contains("kernel helper driver", output);
#endif

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Scroll_Bar_Even_When_The_Charts_Fit()
    {
        ThermalsControl ctrl = CreateChartsOnlyControl();

        ctrl.Sample(SnapshotWith(Sensor(ThermalComponent.Gpu, "111", "GPU", 42)));
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("\u2577", output);
        Assert.DoesNotContain("▲", output);
        Assert.DoesNotContain("▼", output);

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Scroll_Bar_With_The_Empty_State()
    {
        ThermalsControl ctrl = CreateChartsOnlyControl();
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("No thermal sensors detected", output);
        Assert.Contains("\u2577", output);

        ctrl.Unload();
    }

    [Fact]
    public void Gives_Each_Cpu_Cluster_Its_Own_Chart()
    {
        ThermalsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Sensor(ThermalComponent.Cpu, "P-Cores", "Hottest", 67, ThermalSource.Smc),
            Sensor(ThermalComponent.Cpu, "E-Cores", "Hottest", 49, ThermalSource.Smc)));
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("CPU P-Cores   67°C", output);
        Assert.Contains("CPU E-Cores   49°C", output);

        ctrl.Unload();
    }

    [Fact]
    public void Highlights_The_Scroll_Bar_While_Focused()
    {
        ThermalsControl ctrl = CreateControl();

        ctrl.Focused = true;

        Assert.Equal(Control.FocusSelectionColour, ctrl.ScrollBarColour);

        ctrl.Focused = false;

        Assert.Equal(runContext.AppConfig.Theme.ChartBorderForeground, ctrl.ScrollBarColour);

        ctrl.Unload();
    }

#if __APPLE__
    [Fact]
    public void Hosts_A_Fans_Pane_Below_The_Power_Pane()
    {
        ThermalsControl ctrl = CreateControl();

        RaiseSnapshotUpdated(new SystemSnapshot {
            Thermal = new ThermalInfo {
                Metrics = new ThermalMetrics {
                    Fans = [
                        new FanReading { Name = "Fan 1", Rpm = 0,    MinRpm = 1350, MaxRpm = 5349 },
                        new FanReading { Name = "Fan 2", Rpm = 2900, MinRpm = 1458, MaxRpm = 5800 },
                    ]
                }
            }
        });

        FansControl fans   = Assert.Single(ctrl.Controls.OfType<FansControl>());
        PowerControl power = Assert.Single(ctrl.Controls.OfType<PowerControl>());

        Assert.Equal(5, fans.Height);
        Assert.Equal(ctrl.Y + ctrl.Height - fans.Height, fans.Y);
        Assert.Equal(power.X, fans.X);
        Assert.Equal(ctrl.Height - fans.Height, power.Height);

        string output = CapturedOutput();

        Assert.Contains("FANS", output);
        Assert.Contains("Fan 1", output);
        Assert.Contains("Off", output);
        Assert.Contains("50%", output);

        ctrl.Unload();
    }
#endif

    [Fact]
    public void Hosts_A_Power_Pane_By_Default()
    {
        ThermalsControl ctrl = CreateControl();
        ctrl.Draw();

        PowerControl power = Assert.Single(ctrl.Controls.OfType<PowerControl>());

        Assert.Equal(ctrl.X + ctrl.Width - PowerControl.PreferredWidth, power.X);
        Assert.Equal(PowerControl.PreferredWidth, power.Width);
        Assert.Contains("POWER", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Ignores_A_Snapshot_With_No_Thermal_Info()
    {
        ThermalsControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("No thermal sensors detected", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Keeps_Chart_History_When_Navigating_Away_And_Back()
    {
        ThermalsControl ctrl = CreateControl();

        for (int i = 0; i < 5; i++) {
            ctrl.Sample(SnapshotWith(Sensor(ThermalComponent.Gpu, "111", "GPU", 40 + i)));
        }

        Chart chart = Assert.Single(ctrl.Charts.Values);

        ctrl.Unload();
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Load();
        ctrl.Resize();
        ctrl.Draw();

        Assert.Same(chart, Assert.Single(ctrl.Charts.Values));
        Assert.Contains("44°C", CapturedOutput());
        Assert.DoesNotContain("No thermal sensors detected", CapturedOutput());

        ctrl.Unload();
    }

    // ServiceController only raises this from its worker loop, and the fans pane subscribes itself.
    private void RaiseSnapshotUpdated(SystemSnapshot snapshot)
    {
        MulticastDelegate? handler = (MulticastDelegate?)typeof(ServiceController)
            .GetField("SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(runContext.ServiceController);

        handler?.DynamicInvoke(runContext.ServiceController, new SystemSnapshotEventArgs(snapshot));
    }

    [Fact]
    public void Scrolls_Charts_That_Do_Not_Fit()
    {
        // Height 40 / 9 per chart = 4 visible; the sixth component is off screen until PageDown.
        ThermalsControl ctrl = CreateControl(height: 40);

        ctrl.Sample(SnapshotWith(
            Sensor(ThermalComponent.Disk, "0", "Composite", 40, ThermalSource.NvmeHealthLog),
            Sensor(ThermalComponent.Disk, "1", "Composite", 41, ThermalSource.NvmeHealthLog),
            Sensor(ThermalComponent.Disk, "2", "Composite", 42, ThermalSource.NvmeHealthLog),
            Sensor(ThermalComponent.Disk, "3", "Composite", 43, ThermalSource.NvmeHealthLog),
            Sensor(ThermalComponent.Disk, "4", "Composite", 44, ThermalSource.NvmeHealthLog),
            Sensor(ThermalComponent.Disk, "5", "Composite", 45, ThermalSource.NvmeHealthLog)));
        ctrl.Draw();

        string atTop = CapturedOutput();
        Assert.DoesNotContain("Disk 5", atTop);
        Assert.Contains("▼", atTop);           // more below
        Assert.DoesNotContain("▲", atTop);     // nothing above yet

        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.PageDown, false, false, false), ref handled);

        Assert.True(handled);

        string scrolled = CapturedOutput();
        Assert.Contains("Disk 5", scrolled);
        Assert.Contains("▲", scrolled);        // scrolled past the top

        ctrl.Unload();
    }

    private static ThermalSensor Sensor(
        ThermalComponent component, string id, string name, double celsius,
        ThermalSource source = ThermalSource.NvApi) =>
        new() {
            Component = component,
            ComponentId = id,
            SensorName = name,
            Celsius = celsius,
            Source = source
        };

    [Fact]
    public void Shows_Charts_Only_Without_The_Power_Pane()
    {
        ThermalsControl ctrl = CreateChartsOnlyControl();
        ctrl.Draw();

        Assert.Empty(ctrl.Controls.OfType<PowerControl>());
        Assert.Empty(ctrl.Controls.OfType<FansControl>());
        Assert.DoesNotContain("POWER", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Shows_Thermal_Pressure_On_The_Cpu_Charts_Only()
    {
        ThermalsControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot {
            Thermal = new ThermalInfo {
                Metrics = new ThermalMetrics {
                    Sensors  = [
                        Sensor(ThermalComponent.Cpu, "P-Cores", "Hottest", 88, ThermalSource.Smc),
                        Sensor(ThermalComponent.Gpu, "0", "Hottest", 61, ThermalSource.Smc),
                    ],
                    Pressure = ThermalPressure.Heavy
                }
            }
        });

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        int first     = output.IndexOf("Pressure: Heavy", StringComparison.Ordinal);

        Assert.Contains("CPU P-Cores   88°C   Pressure: Heavy", output);
        Assert.Equal(-1, output.IndexOf("Pressure: Heavy", first + 1, StringComparison.Ordinal));

        ctrl.Unload();
    }

    private static SystemSnapshot SnapshotWith(params ThermalSensor[] sensors) =>
        new() { Thermal = new ThermalInfo { Metrics = new ThermalMetrics { Sensors = [.. sensors] } } };
}
