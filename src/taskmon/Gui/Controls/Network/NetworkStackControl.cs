using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Network;

namespace Task.Monitor.Gui.Controls.Network;

public sealed class NetworkStackControl : Control
{
    private const int LinkColumnWidth   = 11;
    private const int MetreColumnWidth  = TextMetreExtensions.TextMetreCells + 1;
    private const int RateColumnWidth   = 12;
    private const int PacketColumnWidth = 12;
    private const int MinNameWidth      = 12;
    private const int BorderAndGutters  = 3;

    private const int SendMetreColumn    = 2;
    private const int ReceiveMetreColumn = 4;

    private const int    RatePeakWindow = 60;
    private const double RatePeakFloor  = 256 * 1024;

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly ListView          networkView;
    private readonly RollingPeak       ratePeak = new(RatePeakWindow, RatePeakFloor);

    public NetworkStackControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        networkView = new ListView(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = false,
            Visible           = true,
            HeaderText        = Strings.NetworkStackHeader,
            EmptyListViewText = Strings.NetworkStackNoAdapters
        };

        networkView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.NetworkStackColumnName))
            .Add(new ListViewColumnHeader(Strings.NetworkStackColumnLink) { RightAligned = true })
            .Add(new ListViewColumnHeader(Strings.NetworkStackColumnSend))
            .Add(new ListViewColumnHeader(Strings.NetworkStackColumnSendRate) { RightAligned = true })
            .Add(new ListViewColumnHeader(Strings.NetworkStackColumnReceive))
            .Add(new ListViewColumnHeader(Strings.NetworkStackColumnReceiveRate) { RightAligned = true })
            .Add(new ListViewColumnHeader(Strings.NetworkStackColumnSendPackets) { RightAligned = true })
            .Add(new ListViewColumnHeader(Strings.NetworkStackColumnReceivePackets) { RightAligned = true });

        Controls.Add(networkView);
    }

    internal static int[] ComputeColumnWidths(int width)
    {
        int available = Math.Max(0, width - BorderAndGutters);
        int values    = LinkColumnWidth + 2 * RateColumnWidth;
        int metre     = available - values - 2 * MetreColumnWidth >= MinNameWidth ? MetreColumnWidth : 0;
        int packets   = available - values - 2 * (MetreColumnWidth + PacketColumnWidth) >= MinNameWidth ? PacketColumnWidth : 0;
        int name      = Math.Max(0, available - values - 2 * (metre + packets));

        return [name, LinkColumnWidth, metre, RateColumnWidth, metre, RateColumnWidth, packets, packets];
    }

    internal ListView NetworkView => networkView;

    protected override void OnDraw()
    {
        networkView.Focused = Focused;
        networkView.Draw();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled) =>
        networkView.KeyPressed(keyInfo, ref handled);

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(networkView, appConfig.Theme);

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        base.OnLoad();
    }

    protected override void OnResize()
    {
        networkView.X      = X;
        networkView.Y      = Y;
        networkView.Width  = Width;
        networkView.Height = Height;

        int[] widths = ComputeColumnWidths(Width);

        for (int i = 0; i < widths.Length; i++) {
            networkView.ColumnHeaders[i].Width = widths[i];
        }

        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        networkView.Items.Clear();
        ratePeak.Clear();

        base.OnUnload();
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Network is not { } network) {
            return;
        }

        int top      = networkView.TopIndex;
        int selected = networkView.SelectedIndex;

        networkView.Items.Clear();

        Theme theme      = appConfig.Theme;
        Color background = theme.ListViewBackground;
        Color foreground = theme.ListViewForeground;

        ratePeak.Add(network.Metrics.Devices
            .Select(device => Math.Max(device.SendBytesPerSecond, device.ReceiveBytesPerSecond))
            .DefaultIfEmpty(0)
            .Max());

        foreach (NetworkDeviceMetrics device in network.Metrics.Devices) {
            NetworkDevice? spec = network.Specs.Devices.FirstOrDefault(candidate => candidate.InterfaceLuid == device.InterfaceLuid);

            ListViewItem row = new(
                [
                    spec?.ToDisplayName() ?? device.ToDisplayName(),
                    spec is { ReceiveLinkSpeed: > 0 } ? spec.ReceiveLinkSpeed.ToLinkSpeed() : Strings.CommonNotAvailable,
                    ratePeak.Scale(device.SendBytesPerSecond).ToTextMetre(),
                    device.ToNetworkSendRate(),
                    ratePeak.Scale(device.ReceiveBytesPerSecond).ToTextMetre(),
                    device.ToNetworkReceiveRate(),
                    device.SendPacketsPerSecond.ToNetworkPacketRate(),
                    device.ReceivePacketsPerSecond.ToNetworkPacketRate()
                ],
                background,
                foreground);

            row.SubItems[SendMetreColumn].ForegroundColor    = theme.StatusOk;
            row.SubItems[ReceiveMetreColumn].ForegroundColor = theme.StatusOk;

            networkView.Items.Add(row);
        }

        if (networkView.Items.Count > 0) {
            networkView.TopIndex      = top;
            networkView.SelectedIndex = Math.Min(selected, networkView.Items.Count - 1);
            networkView.ScrollSelectedIntoView();
        }

        Draw();
    }
}
