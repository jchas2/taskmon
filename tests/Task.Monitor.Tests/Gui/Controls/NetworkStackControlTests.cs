using Task.Monitor.Gui.Controls.Network;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class NetworkStackControlTests
{
    private const double Kilobyte = 1024;

    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public NetworkStackControlTests()
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

    // The bars are measured against the busiest recent transfer, so they keep one colour.
    [Fact]
    public void Colours_The_Send_And_Receive_Metres_In_One_Colour()
    {
        NetworkStackControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith([Spec(1, "Ethernet", 1_000_000_000)], [Metrics(1, sendKb: 4096, receiveKb: 4096)]));

        ListViewItem row = Assert.Single(ctrl.NetworkView.Items.Cast<ListViewItem>());

        Assert.Equal(runContext.AppConfig.Theme.StatusOk, row.SubItems[2].ForegroundColor);
        Assert.Equal(runContext.AppConfig.Theme.StatusOk, row.SubItems[4].ForegroundColor);

        ctrl.Unload();
    }

    // The name gives way first. Then both packet columns go, then both bars, and the speeds stay.
    [Theory]
    [InlineData(120, 36, 11, 12)]
    [InlineData(96,  12, 11, 12)]
    [InlineData(95,  35, 11, 0)]
    [InlineData(72,  12, 11, 0)]
    [InlineData(71,  33, 0,  0)]
    [InlineData(60,  22, 0,  0)]
    [InlineData(0,   0,  0,  0)]
    public void ComputeColumnWidths_Drops_The_Packets_Then_The_Metres_Before_The_Speeds(int width, int name, int metre, int packets)
    {
        Assert.Equal([name, 11, metre, 12, metre, 12, packets, packets], NetworkStackControl.ComputeColumnWidths(width));
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        NetworkStackControl ctrl = new(runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private NetworkStackControl CreateControl(int width = 120, int height = 12)
    {
        NetworkStackControl ctrl = new(
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

    // Ethernet and a WSL switch are up. The Bluetooth adapter is down, so the service has no metrics
    // for it.
    private static SystemSnapshot DesktopSnapshot() =>
        SnapshotWith(
            [
                Spec(1, "Ethernet",                     2_500_000_000),
                Spec(2, "Bluetooth Network Connection", 3_000_000),
                Spec(3, "vEthernet (WSL)",              10_000_000_000),
            ],
            [
                Metrics(1, sendKb: 512, receiveKb: 2048, sentPackets: 412, receivedPackets: 1530.4),
                Metrics(3, sendKb: 1,   receiveKb: 1024, sentPackets: 2,   receivedPackets: 760),
            ]);

    // Ten adapters in a pane that shows two rows: the second Down moves the selection past the
    // bottom, so the list scrolls a row.
    [Fact]
    public void Down_Moves_The_Selection_And_The_Next_Readings_Keep_It()
    {
        NetworkStackControl ctrl = CreateControl(height: 5);

        SystemSnapshot snapshot = SnapshotWith(
            [.. Enumerable.Range(0, 10).Select(i => Spec((ulong)i, $"Ethernet {i}", 1_000_000_000))],
            [.. Enumerable.Range(0, 10).Select(i => Metrics((ulong)i, sendKb: 0, receiveKb: 0))]);

        ctrl.Sample(snapshot);

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);

        Assert.True(handled);
        Assert.Equal(2, ctrl.NetworkView.SelectedIndex);
        Assert.Equal(1, ctrl.NetworkView.TopIndex);

        ctrl.Sample(snapshot);

        Assert.Equal(2, ctrl.NetworkView.SelectedIndex);
        Assert.Equal(1, ctrl.NetworkView.TopIndex);

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Column_Headers_And_Each_Adapter()
    {
        NetworkStackControl ctrl = CreateControl();

        ctrl.Sample(DesktopSnapshot());

        string output = CapturedOutput();

        Assert.Contains("NETWORK", output);
        Assert.Contains("LINK", output);
        Assert.Contains("SEND/S", output);
        Assert.Contains("RECEIVE/S", output);
        Assert.Contains("SEND PKT/S", output);
        Assert.Contains("RECV PKT/S", output);
        Assert.Contains("1,530/s", output);
        Assert.Contains("vEthernet (WSL)", output);
        Assert.Contains("2.5 Gbps", output);
        Assert.Contains("2.0 MB/s", output);

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Empty_Text_Without_Adapters()
    {
        NetworkStackControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("No active network adapters.", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Highlights_The_Table_Border_While_Focused()
    {
        NetworkStackControl ctrl = CreateControl();

        ctrl.Focused = true;
        ctrl.Draw();

        Assert.Equal(Control.FocusSelectionColour, ctrl.NetworkView.DisplayBorderColour);

        ctrl.Focused = false;
        ctrl.Draw();

        Assert.False(ctrl.NetworkView.Focused);

        ctrl.Unload();
    }

    [Fact]
    public void Keeps_Its_Rows_When_A_Snapshot_Has_No_Network_Readings()
    {
        NetworkStackControl ctrl = CreateControl();

        ctrl.Sample(DesktopSnapshot());
        ctrl.Sample(new SystemSnapshot());

        Assert.Equal(2, ctrl.NetworkView.Items.Count);

        ctrl.Unload();
    }

    [Theory]
    [InlineData(ConsoleKey.LeftArrow)]
    [InlineData(ConsoleKey.RightArrow)]
    public void Leaves_Left_And_Right_To_The_Host(ConsoleKey key)
    {
        NetworkStackControl ctrl = CreateControl();
        ctrl.Sample(DesktopSnapshot());

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', key, false, false, false), ref handled);

        Assert.False(handled);

        ctrl.Unload();
    }

    // Only the adapters the service measured are listed, in its order. Ethernet's 2 MB/s receive is
    // the busiest transfer, so it fills its bar and every other send and receive is measured against it.
    [Fact]
    public void Lists_Each_Active_Adapter_With_Its_Readings()
    {
        NetworkStackControl ctrl = CreateControl();

        ctrl.Sample(DesktopSnapshot());

        Assert.Equal(
            [
                ["Ethernet",        "2.5 Gbps", "⣿⣿⣿       ", "512.0 KB/s", "⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿", "2.0 MB/s", "412/s", "1,530/s"],
                ["vEthernet (WSL)", "10 Gbps",  "⣿         ", "1.0 KB/s",   "⣿⣿⣿⣿⣿     ", "1.0 MB/s", "2/s",   "760/s"],
            ],
            Rows(ctrl));

        ctrl.Unload();
    }

    private static NetworkDeviceMetrics Metrics(ulong luid, double sendKb, double receiveKb, double sentPackets = 0, double receivedPackets = 0) =>
        new() {
            InterfaceIndex          = (uint)luid,
            InterfaceLuid           = luid,
            SendBytesPerSecond      = sendKb * Kilobyte,
            ReceiveBytesPerSecond   = receiveKb * Kilobyte,
            SendPacketsPerSecond    = sentPackets,
            ReceivePacketsPerSecond = receivedPackets
        };

    // Under the 256 KB/s floor the bars are measured against the floor, so idle traffic stays
    // close to empty instead of filling the bar.
    [Fact]
    public void Rate_Metres_Keep_Idle_Traffic_Low_Under_The_Floor()
    {
        NetworkStackControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith([Spec(1, "Wi-Fi", 866_700_000)], [Metrics(1, sendKb: 0, receiveKb: 30)]));

        string[] row = Assert.Single(Rows(ctrl));

        Assert.Equal("          ", row[2]);
        Assert.Equal("⣿         ", row[4]);

        ctrl.Unload();
    }

    [Fact]
    public void Rate_Metres_Start_Again_After_A_Reload()
    {
        NetworkStackControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith([Spec(1, "Ethernet", 1_000_000_000)], [Metrics(1, sendKb: 0, receiveKb: 2048)]));
        ctrl.Sample(SnapshotWith([Spec(1, "Ethernet", 1_000_000_000)], [Metrics(1, sendKb: 0, receiveKb: 256)]));

        Assert.Equal("⣿         ", Assert.Single(Rows(ctrl))[4]);

        ctrl.Unload();
        ctrl.Load();

        ctrl.Sample(SnapshotWith([Spec(1, "Ethernet", 1_000_000_000)], [Metrics(1, sendKb: 0, receiveKb: 256)]));

        Assert.Equal("⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿", Assert.Single(Rows(ctrl))[4]);

        ctrl.Unload();
    }

    private static string[][] Rows(NetworkStackControl ctrl) =>
        ctrl.NetworkView.Items
            .Cast<ListViewItem>()
            .Select(row => row.SubItems.Cast<ListViewSubItem>().Select(cell => cell.Text).ToArray())
            .ToArray();

    // An adapter measured before its specs were read has no name or link speed of its own yet.
    [Fact]
    public void Shows_A_Numbered_Name_And_No_Link_Speed_Without_Specs()
    {
        NetworkStackControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith([], [Metrics(9, sendKb: 0, receiveKb: 0)]));

        string[] row = Assert.Single(Rows(ctrl));

        Assert.Equal(("Adapter 9", "N/A"), (row[0], row[1]));

        ctrl.Unload();
    }

    private static SystemSnapshot SnapshotWith(NetworkDevice[] specs, NetworkDeviceMetrics[] metrics) =>
        new() {
            Network = new NetworkInfo {
                Specs   = new NetworkSpecs   { Devices = [.. specs] },
                Metrics = new NetworkMetrics { Devices = [.. metrics] }
            }
        };

    private static NetworkDevice Spec(ulong luid, string friendlyName, ulong linkBitsPerSecond) =>
        new() {
            InterfaceIndex    = (uint)luid,
            InterfaceLuid     = luid,
            FriendlyName      = friendlyName,
            TransmitLinkSpeed = linkBitsPerSecond,
            ReceiveLinkSpeed  = linkBitsPerSecond,
            IsActive          = true
        };
}
