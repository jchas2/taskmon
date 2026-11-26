using Task.Monitor.Gui.Controls.Gpu;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class GpuStackControlTests
{
    private const long NvidiaLuid = 111;
    private const long IntelLuid  = 222;

    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public GpuStackControlTests()
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
    public void Colours_Each_Metre_By_Its_Load()
    {
        GpuStackControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot {
            Gpu = new GpuInfo {
                Specs   = new GpuSpecs   { Devices = [Device(0, NvidiaLuid, "NVIDIA GeForce RTX 4070 SUPER")] },
                Metrics = new GpuMetrics { Devices = [Metrics(0, NvidiaLuid, gpu: 0.9, memory: 0.6)] }
            }
        });

        ListViewItem row = Assert.Single(ctrl.GpuView.Items.Cast<ListViewItem>());

        Assert.Equal(runContext.AppConfig.Theme.StatusError,   row.SubItems[4].ForegroundColor);
        Assert.Equal(runContext.AppConfig.Theme.StatusWarning, row.SubItems[6].ForegroundColor);

        ctrl.Unload();
    }

    // The name gives way first, then both metres go together, and the percentages stay.
    [Theory]
    [InlineData(120, 57, 11)]
    [InlineData(75,  12, 11)]
    [InlineData(74,  33, 0)]
    [InlineData(40,  0,  0)]
    [InlineData(0,   0,  0)]
    public void ComputeColumnWidths_Drops_The_Metres_Before_The_Percentages(int width, int name, int metre)
    {
        Assert.Equal([7, name, 7, 10, metre, 7, metre, 7], GpuStackControl.ComputeColumnWidths(width));
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        GpuStackControl ctrl = new(runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private GpuStackControl CreateControl(int width = 120, int height = 12)
    {
        GpuStackControl ctrl = new(
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

    // A desktop with an NVIDIA card and Intel integrated graphics, listed out of order. Only the
    // NVIDIA card has temperature and power readings.
    private static SystemSnapshot DesktopSnapshot() =>
        new() {
            Gpu = new GpuInfo {
                Specs = new GpuSpecs {
                    Devices = [
                        Device(1, IntelLuid,  "Intel(R) UHD Graphics 770"),
                        Device(0, NvidiaLuid, "NVIDIA GeForce RTX 4070 SUPER"),
                    ]
                },
                Metrics = new GpuMetrics {
                    Devices = [
                        Metrics(0, NvidiaLuid, gpu: 0.232, memory: 0.066),
                        Metrics(1, IntelLuid,  gpu: 0.021, memory: 0.613),
                    ]
                }
            },
            Thermal = new ThermalInfo {
                Metrics = new ThermalMetrics {
                    Sensors = [new ThermalSensor { Component = ThermalComponent.Gpu, ComponentId = $"{NvidiaLuid}", SensorName = "GPU", Celsius = 52, Source = ThermalSource.NvApi }]
                }
            },
            Power = new PowerInfo {
                Metrics = new PowerMetrics {
                    Readings = [new PowerReading { Component = PowerComponent.Gpu, ComponentId = $"{NvidiaLuid}", Rail = PowerRails.Gpu, Watts = 88.4 }]
                }
            }
        };

    private static GpuDevice Device(int index, long luid, string description) =>
        new() { Index = index, AdapterLuid = luid, Description = description };

    // Ten GPUs in a pane that shows two rows: the second Down moves the selection past the bottom,
    // so the list scrolls a row.
    [Fact]
    public void Down_Moves_The_Selection_And_The_Next_Readings_Keep_It()
    {
        GpuStackControl ctrl = CreateControl(height: 5);

        SystemSnapshot snapshot = new() {
            Gpu = new GpuInfo {
                Specs = new GpuSpecs { Devices = [.. Enumerable.Range(0, 10).Select(i => Device(i, 100 + i, $"GPU model {i}"))] }
            }
        };

        ctrl.Sample(snapshot);

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);

        Assert.True(handled);
        Assert.Equal(2, ctrl.GpuView.SelectedIndex);
        Assert.Equal(1, ctrl.GpuView.TopIndex);

        ctrl.Sample(snapshot);

        Assert.Equal(2, ctrl.GpuView.SelectedIndex);
        Assert.Equal(1, ctrl.GpuView.TopIndex);

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Column_Headers_And_Each_Gpu()
    {
        GpuStackControl ctrl = CreateControl();

        ctrl.Sample(DesktopSnapshot());

        string output = CapturedOutput();

        Assert.Contains("GPUS", output);
        Assert.Contains("TEMP", output);
        Assert.Contains("POWER", output);
        Assert.Contains("GPU%", output);
        Assert.Contains("MEM%", output);
        Assert.Contains("NVIDIA GeForce RTX 4070 SUPER", output);
        Assert.Contains("88.4 W", output);
        Assert.Contains("023.2%", output);

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Empty_Text_Without_Gpus()
    {
        GpuStackControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("No GPUs found.", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Highlights_The_Table_Border_While_Focused()
    {
        GpuStackControl ctrl = CreateControl();

        ctrl.Focused = true;
        ctrl.Draw();

        Assert.Equal(Control.FocusSelectionColour, ctrl.GpuView.DisplayBorderColour);

        ctrl.Focused = false;
        ctrl.Draw();

        Assert.False(ctrl.GpuView.Focused);

        ctrl.Unload();
    }

    [Fact]
    public void Keeps_Its_Rows_When_A_Snapshot_Has_No_Gpu_Readings()
    {
        GpuStackControl ctrl = CreateControl();

        ctrl.Sample(DesktopSnapshot());
        ctrl.Sample(new SystemSnapshot());

        Assert.Equal(2, ctrl.GpuView.Items.Count);

        ctrl.Unload();
    }

    [Theory]
    [InlineData(ConsoleKey.LeftArrow)]
    [InlineData(ConsoleKey.RightArrow)]
    public void Leaves_Left_And_Right_To_The_Host(ConsoleKey key)
    {
        GpuStackControl ctrl = CreateControl();
        ctrl.Sample(DesktopSnapshot());

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', key, false, false, false), ref handled);

        Assert.False(handled);

        ctrl.Unload();
    }

    // The Intel GPU has no temperature or power reading, and its light load still shows one cell.
    [Fact]
    public void Lists_Each_Gpu_In_Index_Order_With_Its_Readings()
    {
        GpuStackControl ctrl = CreateControl();

        ctrl.Sample(DesktopSnapshot());

        Assert.Equal(
            [
                ["GPU 0", "NVIDIA GeForce RTX 4070 SUPER", "52°C", "88.4 W", "⣿⣿        ", "023.2%", "⣿         ", "006.6%"],
                ["GPU 1", "Intel(R) UHD Graphics 770",     "N/A",  "N/A",    "⣿         ", "002.1%", "⣿⣿⣿⣿⣿⣿    ", "061.3%"],
            ],
            Rows(ctrl));

        ctrl.Unload();
    }

    private static GpuDeviceMetrics Metrics(int index, long luid, double gpu, double memory) =>
        new() {
            Index              = index,
            AdapterLuid        = luid,
            GpuPercentTime     = gpu,
            TotalGpuMemory     = 1000,
            AvailableGpuMemory = (long)Math.Round(1000 * (1 - memory))
        };

    private static string[][] Rows(GpuStackControl ctrl) =>
        ctrl.GpuView.Items
            .Cast<ListViewItem>()
            .Select(row => row.SubItems.Cast<ListViewSubItem>().Select(cell => cell.Text).ToArray())
            .ToArray();

    // On Windows a GPU whose memory statistics can't be read has no metrics, so its row has no
    // metres and no percentages.
    [Fact]
    public void Shows_Not_Available_For_A_Gpu_Without_Metrics()
    {
        GpuStackControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot {
            Gpu = new GpuInfo { Specs = new GpuSpecs { Devices = [Device(0, NvidiaLuid, "NVIDIA GeForce RTX 4070 SUPER")] } }
        });

        Assert.Equal(
            [["GPU 0", "NVIDIA GeForce RTX 4070 SUPER", "N/A", "N/A", "", "N/A", "", "N/A"]],
            Rows(ctrl));

        ctrl.Unload();
    }
}
