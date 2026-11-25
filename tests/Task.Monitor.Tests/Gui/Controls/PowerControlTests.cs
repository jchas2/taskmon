using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
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

    // The Windows readings at the THERMALS side pane's width: each sized column fits its widest entry
    // ("COMPONENT", "Drive", "888.8 W (rated)") and its spacing, and the name takes the other 35,
    // which shows the GPU's whole name.
    [Fact]
    public void Columns_Fit_Their_Widest_Entry_And_The_Name_Takes_The_Rest()
    {
        PowerControl ctrl = CreateControl();

        ctrl.Sample(WindowsSnapshot(22.8));

        Assert.Equal([11, 35, 7, 16], ctrl.PowerView.ColumnHeaders.Select(column => column.Width));
        Assert.Contains("NVIDIA GeForce RTX 4070 SUPER", CapturedOutput());

        ctrl.Unload();
    }

    [Theory]
    [InlineData(60, 10, 6,  16, 10, 25, 6,  16)]
    [InlineData(56, 14, 17, 8,  14, 14, 17, 8)]
    [InlineData(40, 14, 17, 8,  14, 0,  15, 8)]
    [InlineData(20, 14, 17, 8,  9,  0,  0,  8)]
    [InlineData(8,  14, 17, 8,  0,  0,  0,  5)]
    public void ComputeColumnWidths_Gives_Up_The_Name_Then_The_Rail_Then_The_Component(
        int width, int fitComponent, int fitRail, int fitPower, int component, int name, int rail, int power)
    {
        (int Component, int Name, int Rail, int Power) widths = PowerControl.ComputeColumnWidths(width, fitComponent, fitRail, fitPower);

        Assert.Equal((component, name, rail, power), widths);
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

    private static (string Component, string Name, string Rail, string Power)[] Rows(PowerControl ctrl) =>
        ctrl.PowerView.Items
            .Cast<ListViewItem>()
            .Select(row => (row.SubItems[0].Text, row.SubItems[1].Text, row.SubItems[2].Text, row.SubItems[3].Text))
            .ToArray();

    [Fact]
    public void Separates_Each_Component_From_Its_Name()
    {
        PowerControl ctrl = CreateControl(width: 100);

        ctrl.Sample(WindowsSnapshot(22.8));

        Assert.Equal(
            [
                ("GPU",    "NVIDIA GeForce RTX 4070 SUPER", "GPU",   "22.8 W"),
                ("Disk 0", "KINGSTON SKC3000D2048G",        "Drive", "8.8 W (rated)"),
            ],
            Rows(ctrl));

        string output = CapturedOutput();

        Assert.Contains("NAME", output);
        Assert.Contains("NVIDIA GeForce RTX 4070 SUPER", output);
        Assert.Contains("KINGSTON SKC3000D2048G", output);

        ctrl.Unload();
    }

    // A reading that gains a digit mustn't widen the power column and shift the others.
    [Fact]
    public void The_Power_Column_Holds_Its_Width_As_Readings_Change()
    {
        PowerControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot { Power = new PowerInfo { Metrics = new PowerMetrics { Readings = [Power(PowerComponent.Cpu, PowerRails.Cpu, 9.9)] } } });
        int[] before = ctrl.PowerView.ColumnHeaders.Select(column => column.Width).ToArray();

        ctrl.Sample(new SystemSnapshot { Power = new PowerInfo { Metrics = new PowerMetrics { Readings = [Power(PowerComponent.Cpu, PowerRails.Cpu, 123.4)] } } });

        Assert.Equal(before, ctrl.PowerView.ColumnHeaders.Select(column => column.Width));
        Assert.Equal(8, before[3]);

        ctrl.Unload();
    }

    // A desktop with one GPU and one NVMe drive, as the Windows power and specs services report them.
    private static SystemSnapshot WindowsSnapshot(double gpuWatts) =>
        new() {
            Gpu   = new GpuInfo  { Specs = new GpuSpecs  { Devices = [new GpuDevice  { AdapterLuid = 111, Description = "NVIDIA GeForce RTX 4070 SUPER" }] } },
            Disk  = new DiskInfo { Specs = new DiskSpecs { Devices = [new DiskDevice { Index = 0, Model = "KINGSTON SKC3000D2048G" }] } },
            Power = new PowerInfo {
                Metrics = new PowerMetrics {
                    Readings = [
                        Power(PowerComponent.Disk, PowerRails.Drive, 8.8,      componentId: "0", rated: true),
                        Power(PowerComponent.Gpu,  PowerRails.Gpu,   gpuWatts, componentId: "111"),
                    ]
                }
            }
        };
}
