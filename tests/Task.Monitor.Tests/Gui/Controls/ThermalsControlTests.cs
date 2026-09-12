using Task.Monitor.Gui.Controls.Thermals;
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

    private static SystemSnapshot SnapshotWith(params ThermalSensor[] sensors) =>
        new() { Thermal = new ThermalInfo { Metrics = new ThermalMetrics { Sensors = [.. sensors] } } };

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

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        ThermalsControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Draws_The_Empty_State_When_There_Are_No_Sensors()
    {
        ThermalsControl ctrl = CreateControl();
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("No thermal sensors detected", output);
        Assert.Contains("kernel helper driver", output);

        ctrl.Unload();
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

    [Fact]
    public void Ignores_A_Snapshot_With_No_Thermal_Info()
    {
        ThermalsControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("No thermal sensors detected", CapturedOutput());

        ctrl.Unload();
    }
}
