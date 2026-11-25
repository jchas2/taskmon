using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ThermalListControlTests
{
    private const long GpuLuid = 111;

    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public ThermalListControlTests()
    {
        runContextHelper = new RunContextHelper();
        runContext       = runContextHelper.GetRunContext();
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Collapses_A_Multi_Sensor_Drive_To_One_Row_Showing_The_Composite()
    {
        ThermalListControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Sensor(ThermalComponent.Disk, "0", "Composite", 35, ThermalSource.NvmeHealthLog),
            Sensor(ThermalComponent.Disk, "0", "Sensor 2",  63, ThermalSource.NvmeHealthLog)));

        Assert.Equal([("Disk 0", "KINGSTON SKC3000D2048G", "35°C")], Rows(ctrl));

        ctrl.Unload();
    }

    [Theory]
    [InlineData(60, 15, 35, 7)]
    [InlineData(25, 15, 0,  7)]
    [InlineData(15, 5,  0,  7)]
    [InlineData(8,  0,  0,  5)]
    [InlineData(2,  0,  0,  0)]
    public void ComputeColumnWidths_Gives_Up_The_Name_Before_The_Temperature(int width, int component, int name, int temperature)
    {
        (int Component, int Name, int Temperature) widths = ThermalListControl.ComputeColumnWidths(width);

        Assert.Equal((component, name, temperature), widths);
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        ThermalListControl ctrl = new(runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private ThermalListControl CreateControl(int width = 70, int height = 12)
    {
        ThermalListControl ctrl = new(
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
    public void Draws_Each_Component_With_Its_Name_And_Temperature()
    {
        ThermalListControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Sensor(ThermalComponent.Other, "",           "ACPI TZ00", 28, ThermalSource.AcpiThermalZone),
            Sensor(ThermalComponent.Disk,  "0",          "Composite", 38, ThermalSource.NvmeHealthLog),
            Sensor(ThermalComponent.Gpu,   $"{GpuLuid}", "GPU",       45)));

        string output = CapturedOutput();

        Assert.Contains("THERMALS", output);
        Assert.Contains("COMPONENT", output);
        Assert.Contains("NVIDIA GeForce RTX 4070 SUPER", output);
        Assert.Contains("45°C", output);
        Assert.Contains("KINGSTON SKC3000D2048G", output);
        Assert.Contains("28°C", output);

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Empty_Text_Without_Sensors()
    {
        ThermalListControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("No thermal sensors detected.", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Highlights_The_Table_Border_While_Focused()
    {
        ThermalListControl ctrl = CreateControl();

        ctrl.Focused = true;
        ctrl.Draw();

        Assert.Equal(Control.FocusSelectionColour, ctrl.ThermalView.DisplayBorderColour);

        ctrl.Focused = false;
        ctrl.Draw();

        Assert.False(ctrl.ThermalView.Focused);

        ctrl.Unload();
    }

    [Fact]
    public void Keeps_Its_Rows_When_A_Snapshot_Has_No_Thermal_Readings()
    {
        ThermalListControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(Sensor(ThermalComponent.Gpu, $"{GpuLuid}", "GPU", 45)));
        ctrl.Sample(new SystemSnapshot());

        Assert.Equal([("GPU", "NVIDIA GeForce RTX 4070 SUPER", "45°C")], Rows(ctrl));

        ctrl.Unload();
    }

    [Fact]
    public void Leaves_The_Name_Blank_Without_A_Matching_Device()
    {
        ThermalListControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot {
            Thermal = new ThermalInfo {
                Metrics = new ThermalMetrics {
                    Sensors = [
                        Sensor(ThermalComponent.Gpu,     "nvml0", "GPU",     50),
                        Sensor(ThermalComponent.Disk,    "3",     "Drive",   41, ThermalSource.AtaSmart),
                        Sensor(ThermalComponent.Battery, "",      "Battery", 30, ThermalSource.Smc),
                    ]
                }
            }
        });

        Assert.Equal(
            [
                ("GPU",     "", "50°C"),
                ("Disk 3",  "", "41°C"),
                ("Battery", "", "30°C"),
            ],
            Rows(ctrl));

        ctrl.Unload();
    }

    [Fact]
    public void Lists_Components_In_Chart_Order_And_Names_The_Cpu()
    {
        ThermalListControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Sensor(ThermalComponent.Other, "",           "ACPI TZ00", 28, ThermalSource.AcpiThermalZone),
            Sensor(ThermalComponent.Disk,  "0",          "Composite", 38, ThermalSource.NvmeHealthLog),
            Sensor(ThermalComponent.Gpu,   $"{GpuLuid}", "GPU",       45),
            Sensor(ThermalComponent.Cpu,   "",           "ACPI CPU0", 55, ThermalSource.AcpiThermalZone)));

        Assert.Equal(
            [
                ("CPU",    "AMD Ryzen 7 7800X3D",           "55°C"),
                ("GPU",    "NVIDIA GeForce RTX 4070 SUPER", "45°C"),
                ("Disk 0", "KINGSTON SKC3000D2048G",        "38°C"),
                ("System", "",                              "28°C"),
            ],
            Rows(ctrl));

        ctrl.Unload();
    }

    private static (string Component, string Name, string Temperature)[] Rows(ThermalListControl ctrl) =>
        ctrl.ThermalView.Items
            .Cast<ListViewItem>()
            .Select(row => (row.SubItems[0].Text, row.SubItems[1].Text, row.SubItems[2].Text))
            .ToArray();

    private static ThermalSensor Sensor(
        ThermalComponent component, string id, string name, double celsius,
        ThermalSource source = ThermalSource.NvApi) =>
        new() {
            Component   = component,
            ComponentId = id,
            SensorName  = name,
            Celsius     = celsius,
            Source      = source
        };

    // A desktop with one GPU and one NVMe drive, as the specs services report them.
    private static SystemSnapshot SnapshotWith(params ThermalSensor[] sensors) =>
        new() {
            Cpu     = new CpuInfo     { Specs   = new CpuSpecs  { CpuName = "AMD Ryzen 7 7800X3D" } },
            Gpu     = new GpuInfo     { Specs   = new GpuSpecs  { Devices = [new GpuDevice  { AdapterLuid = GpuLuid, Description = "NVIDIA GeForce RTX 4070 SUPER" }] } },
            Disk    = new DiskInfo    { Specs   = new DiskSpecs { Devices = [new DiskDevice { Index = 0, Model = "KINGSTON SKC3000D2048G" }] } },
            Thermal = new ThermalInfo { Metrics = new ThermalMetrics { Sensors = [.. sensors] } }
        };
}
