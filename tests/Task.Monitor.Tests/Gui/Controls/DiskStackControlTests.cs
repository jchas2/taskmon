using Task.Monitor.Gui.Controls.Disk;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class DiskStackControlTests
{
    private const double Megabyte = 1024 * 1024;
    private const int    IdWidth  = 8;

    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public DiskStackControlTests()
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

    // Active time is a load of its own, so its bar takes the load colours. The read and write bars
    // are measured against the busiest recent transfer, so they keep one colour.
    [Fact]
    public void Colours_The_Active_Metre_By_Its_Load_And_The_Rate_Metres_In_One_Colour()
    {
        DiskStackControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith([Device(0, "KINGSTON SKC3000D2048G")], [Metrics(0, active: 0.9, readMb: 400, writeMb: 400)]));

        ListViewItem row = Assert.Single(ctrl.DiskView.Items.Cast<ListViewItem>());

        Assert.Equal(runContext.AppConfig.Theme.StatusError, row.SubItems[3].ForegroundColor);
        Assert.Equal(runContext.AppConfig.Theme.StatusOk,    row.SubItems[5].ForegroundColor);
        Assert.Equal(runContext.AppConfig.Theme.StatusOk,    row.SubItems[7].ForegroundColor);

        ctrl.Unload();
    }

    // The name gives way first. Then the read and write bars go, then the active bar, and the
    // values stay.
    [Theory]
    [InlineData(120, 38, 11, 11)]
    [InlineData(94,  12, 11, 11)]
    [InlineData(93,  33, 11, 0)]
    [InlineData(72,  12, 11, 0)]
    [InlineData(71,  22, 0,  0)]
    [InlineData(60,  11, 0,  0)]
    [InlineData(0,   0,  0,  0)]
    public void ComputeColumnWidths_Drops_The_Read_And_Write_Metres_Before_The_Active_Metre(int width, int name, int active, int rate)
    {
        Assert.Equal([IdWidth, name, 7, active, 7, rate, 12, rate, 12], DiskStackControl.ComputeColumnWidths(width, IdWidth));
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        DiskStackControl ctrl = new(runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private DiskStackControl CreateControl(int width = 120, int height = 12)
    {
        DiskStackControl ctrl = new(
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

    // An NVMe drive and a hard disk, listed out of order. Only the NVMe drive has a temperature.
    private static SystemSnapshot DesktopSnapshot() =>
        SnapshotWith(
            [Device(1, "ST4000DM004-2CV104"), Device(0, "KINGSTON SKC3000D2048G")],
            [Metrics(0, active: 0.12, readMb: 100, writeMb: 50), Metrics(1, active: 0.885, readMb: 25, writeMb: 10)])
        with {
            Thermal = new ThermalInfo {
                Metrics = new ThermalMetrics {
                    Sensors = [new ThermalSensor { Component = ThermalComponent.Disk, ComponentId = "0", SensorName = "Composite", Celsius = 38, Source = ThermalSource.NvmeHealthLog }]
                }
            }
        };

    private static DiskDevice Device(int index, string model) =>
        new() { Index = index, Model = model };

    // Ten disks in a pane that shows two rows: the second Down moves the selection past the bottom,
    // so the list scrolls a row.
    [Fact]
    public void Down_Moves_The_Selection_And_The_Next_Readings_Keep_It()
    {
        DiskStackControl ctrl = CreateControl(height: 5);

        SystemSnapshot snapshot = SnapshotWith([.. Enumerable.Range(0, 10).Select(i => Device(i, $"Disk model {i}"))], []);

        ctrl.Sample(snapshot);

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);

        Assert.True(handled);
        Assert.Equal(2, ctrl.DiskView.SelectedIndex);
        Assert.Equal(1, ctrl.DiskView.TopIndex);

        ctrl.Sample(snapshot);

        Assert.Equal(2, ctrl.DiskView.SelectedIndex);
        Assert.Equal(1, ctrl.DiskView.TopIndex);

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Column_Headers_And_Each_Disk()
    {
        DiskStackControl ctrl = CreateControl();

        ctrl.Sample(DesktopSnapshot());

        string output = CapturedOutput();

        Assert.Contains("DISKS", output);
        Assert.Contains("ACTIVE", output);
        Assert.Contains("ACT%", output);
        Assert.Contains("READ/S", output);
        Assert.Contains("WRITE/S", output);
        Assert.Contains("KINGSTON SKC3000D2048G", output);
        Assert.Contains("100.0 MB/s", output);
        Assert.Contains("088.5%", output);

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Empty_Text_Without_Disks()
    {
        DiskStackControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("No disks found.", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Highlights_The_Table_Border_While_Focused()
    {
        DiskStackControl ctrl = CreateControl();

        ctrl.Focused = true;
        ctrl.Draw();

        Assert.Equal(Control.FocusSelectionColour, ctrl.DiskView.DisplayBorderColour);

        ctrl.Focused = false;
        ctrl.Draw();

        Assert.False(ctrl.DiskView.Focused);

        ctrl.Unload();
    }

    // "Datenträger 10" is fourteen characters, so the ID column grows to fit it rather than cutting it.
    [Theory]
    [InlineData(UiCulture.British, 8)]
    [InlineData(UiCulture.Spanish, 9)]
    [InlineData(UiCulture.German,  15)]
    public void IdColumnWidth_Fits_A_Two_Digit_Disk_Label_In_The_UI_Language(string cultureName, int expected)
    {
        using IDisposable _ = UiCulture.Use(cultureName);

        Assert.Equal(expected, DiskStackControl.IdColumnWidth());
    }

    [Fact]
    public void Keeps_Its_Rows_When_A_Snapshot_Has_No_Disk_Readings()
    {
        DiskStackControl ctrl = CreateControl();

        ctrl.Sample(DesktopSnapshot());
        ctrl.Sample(new SystemSnapshot());

        Assert.Equal(2, ctrl.DiskView.Items.Count);

        ctrl.Unload();
    }

    [Theory]
    [InlineData(ConsoleKey.LeftArrow)]
    [InlineData(ConsoleKey.RightArrow)]
    public void Leaves_Left_And_Right_To_The_Host(ConsoleKey key)
    {
        DiskStackControl ctrl = CreateControl();
        ctrl.Sample(DesktopSnapshot());

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', key, false, false, false), ref handled);

        Assert.False(handled);

        ctrl.Unload();
    }

    // The NVMe drive's 100 MB/s read is the busiest transfer, so it fills its bar and every other
    // read and write is measured against it.
    [Fact]
    public void Lists_Each_Disk_In_Index_Order_With_Its_Readings()
    {
        DiskStackControl ctrl = CreateControl();

        ctrl.Sample(DesktopSnapshot());

        Assert.Equal(
            [
                ["Disk 0", "KINGSTON SKC3000D2048G", "38°C", "⣿         ", "012.0%", "⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿", "100.0 MB/s", "⣿⣿⣿⣿⣿     ", "50.0 MB/s"],
                ["Disk 1", "ST4000DM004-2CV104",     "N/A",  "⣿⣿⣿⣿⣿⣿⣿⣿⣿ ", "088.5%", "⣿⣿⣿       ", "25.0 MB/s",  "⣿         ", "10.0 MB/s"],
            ],
            Rows(ctrl));

        ctrl.Unload();
    }

    private static DiskDeviceMetrics Metrics(int index, double active, double readMb, double writeMb) =>
        new() {
            Index               = index,
            PercentActiveTime   = active * 100,
            ReadBytesPerSecond  = readMb * Megabyte,
            WriteBytesPerSecond = writeMb * Megabyte
        };

    // Below the 1 MB/s floor the bars are measured against the floor, so background writes stay
    // close to empty instead of filling the bar.
    [Fact]
    public void Rate_Metres_Keep_Light_Traffic_Low_Under_The_Floor()
    {
        DiskStackControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith([Device(0, "KINGSTON SKC3000D2048G")], [Metrics(0, active: 0.01, readMb: 0, writeMb: 0.1)]));

        string[] row = Assert.Single(Rows(ctrl));

        Assert.Equal("          ", row[5]);
        Assert.Equal("⣿         ", row[7]);

        ctrl.Unload();
    }

    [Fact]
    public void Rate_Metres_Start_Again_After_A_Reload()
    {
        DiskStackControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith([Device(0, "KINGSTON SKC3000D2048G")], [Metrics(0, active: 0.5, readMb: 100, writeMb: 0)]));
        ctrl.Sample(SnapshotWith([Device(0, "KINGSTON SKC3000D2048G")], [Metrics(0, active: 0.5, readMb: 10, writeMb: 0)]));

        Assert.Equal("⣿         ", Assert.Single(Rows(ctrl))[5]);

        ctrl.Unload();
        ctrl.Load();

        ctrl.Sample(SnapshotWith([Device(0, "KINGSTON SKC3000D2048G")], [Metrics(0, active: 0.5, readMb: 10, writeMb: 0)]));

        Assert.Equal("⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿", Assert.Single(Rows(ctrl))[5]);

        ctrl.Unload();
    }

    private static string[][] Rows(DiskStackControl ctrl) =>
        ctrl.DiskView.Items
            .Cast<ListViewItem>()
            .Select(row => row.SubItems.Cast<ListViewSubItem>().Select(cell => cell.Text).ToArray())
            .ToArray();

    [Fact]
    public void Shows_Not_Available_For_A_Disk_Without_Metrics()
    {
        DiskStackControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith([Device(0, "KINGSTON SKC3000D2048G")], []));

        Assert.Equal(
            [["Disk 0", "KINGSTON SKC3000D2048G", "N/A", "", "N/A", "", "N/A", "", "N/A"]],
            Rows(ctrl));

        ctrl.Unload();
    }

    private static SystemSnapshot SnapshotWith(DiskDevice[] devices, DiskDeviceMetrics[] metrics) =>
        new() {
            Disk = new DiskInfo {
                Specs   = new DiskSpecs   { Devices = [.. devices] },
                Metrics = new DiskMetrics { Devices = [.. metrics] }
            }
        };
}
