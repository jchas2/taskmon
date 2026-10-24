using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class PowerControlTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public PowerControlTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Theory]
    [InlineData(56, 20, 18, 15)]
    [InlineData(80, 20, 18, 39)]
    [InlineData(41, 14, 14, 10)]
    [InlineData(30, 8,  9,  10)]
    [InlineData(10, 0,  0,  7)]
    public void ComputeColumnWidths_Keeps_The_Power_Column_In_A_Narrow_Pane(int width, int component, int rail, int power)
    {
        (int Component, int Rail, int Power) widths = PowerControl.ComputeColumnWidths(width);

        Assert.Equal((component, rail, power), widths);
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        PowerControl ctrl = new(runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private PowerControl CreateControl(int width = PowerControl.PreferredWidth, int height = 20)
    {
        PowerControl ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width  = width,
            Height = height
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    [Fact]
    public void Draws_Each_Reading_With_System_First()
    {
        PowerControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot {
            Power = new PowerInfo {
                Metrics = new PowerMetrics {
                    Readings = [
                        Power(PowerComponent.Cpu,          PowerRails.PerformanceCores, 1.9),
                        Power(PowerComponent.NeuralEngine, PowerRails.NeuralEngine,     0.0),
                        Power(PowerComponent.System,       PowerRails.SystemBattery,    9.6),
                        Power(PowerComponent.Disk,         "Drive",                     5.5, componentId: "0", rated: true),
                    ]
                }
            }
        });

        string output = CapturedOutput();

        Assert.Contains("POWER", output);
        Assert.Contains("P-Cores", output);
        Assert.Contains("1.9 W", output);
        Assert.Contains("Neural Engine", output);
        Assert.Contains("System (battery)", output);
        Assert.Contains("5.5 W (rated)", output);
        Assert.True(output.IndexOf("System (battery)", StringComparison.Ordinal) < output.IndexOf("P-Cores", StringComparison.Ordinal));

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Empty_Text_Without_Readings()
    {
        PowerControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("No power readings available.", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Highlights_The_Table_Border_While_Focused()
    {
        PowerControl ctrl = CreateControl();

        ctrl.Focused = true;
        ctrl.Draw();

        Assert.Equal(Control.FocusSelectionColour, ctrl.PowerView.DisplayBorderColour);

        ctrl.Focused = false;
        ctrl.Draw();

        Assert.False(ctrl.PowerView.Focused);

        ctrl.Unload();
    }

    private static PowerReading Power(
        PowerComponent component, string rail, double watts, string componentId = "", bool rated = false) =>
        new() {
            Component   = component,
            ComponentId = componentId,
            Rail        = rail,
            Watts       = watts,
            IsRated     = rated,
            Source      = PowerSource.IOReport
        };

}
