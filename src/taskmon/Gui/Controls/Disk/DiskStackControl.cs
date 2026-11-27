using System.Drawing;
using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.Gui.Controls.Disk;

public sealed class DiskStackControl : Control
{
    private const int TemperatureColumnWidth = 7;
    private const int MetreColumnWidth       = TextMetreExtensions.TextMetreCells + 1;
    private const int PercentColumnWidth     = 7;
    private const int RateColumnWidth        = 12;
    private const int MinNameWidth           = 12;
    private const int BorderAndGutters       = 3;

    private const int ActiveMetreColumn = 3;
    private const int ReadMetreColumn   = 5;
    private const int WriteMetreColumn  = 7;

    private const int    RatePeakWindow = 60;
    private const double RatePeakFloor  = 1024 * 1024;

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly ListView          diskView;
    private readonly RollingPeak       ratePeak = new(RatePeakWindow, RatePeakFloor);
    private readonly int               idWidth;

    public DiskStackControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        idWidth = IdColumnWidth();

        diskView = new ListView(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = false,
            Visible           = true,
            HeaderText        = Strings.DiskStackHeader,
            EmptyListViewText = Strings.DiskStackNoDisks
        };

        diskView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.DiskStackColumnId))
            .Add(new ListViewColumnHeader(Strings.DiskStackColumnName))
            .Add(new ListViewColumnHeader(Strings.DiskStackColumnTemperature))
            .Add(new ListViewColumnHeader(Strings.DiskStackColumnActive))
            .Add(new ListViewColumnHeader(Strings.DiskStackColumnActivePercent))
            .Add(new ListViewColumnHeader(Strings.DiskStackColumnRead))
            .Add(new ListViewColumnHeader(Strings.DiskStackColumnReadRate) { RightAligned = true })
            .Add(new ListViewColumnHeader(Strings.DiskStackColumnWrite))
            .Add(new ListViewColumnHeader(Strings.DiskStackColumnWriteRate) { RightAligned = true });

        Controls.Add(diskView);
    }

    internal static int[] ComputeColumnWidths(int width, int idWidth)
    {
        int available   = Math.Max(0, width - BorderAndGutters);
        int values      = idWidth + TemperatureColumnWidth + PercentColumnWidth + 2 * RateColumnWidth;
        int activeMetre = available - values - MetreColumnWidth >= MinNameWidth ? MetreColumnWidth : 0;
        int rateMetre   = available - values - 3 * MetreColumnWidth >= MinNameWidth ? MetreColumnWidth : 0;
        int name        = Math.Max(0, available - values - activeMetre - 2 * rateMetre);

        return [idWidth, name, TemperatureColumnWidth, activeMetre, PercentColumnWidth, rateMetre, RateColumnWidth, rateMetre, RateColumnWidth];
    }

    internal ListView DiskView => diskView;

    internal static int IdColumnWidth() =>
        string.Format(CultureInfo.CurrentCulture, Strings.SnapshotDiskLabelShort, 10).TerminalWidth() + 1;

    protected override void OnDraw()
    {
        diskView.Focused = Focused;
        diskView.Draw();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled) =>
        diskView.KeyPressed(keyInfo, ref handled);

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(diskView, appConfig.Theme);

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        base.OnLoad();
    }

    protected override void OnResize()
    {
        diskView.X      = X;
        diskView.Y      = Y;
        diskView.Width  = Width;
        diskView.Height = Height;

        int[] widths = ComputeColumnWidths(Width, idWidth);

        for (int i = 0; i < widths.Length; i++) {
            diskView.ColumnHeaders[i].Width = widths[i];
        }

        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        diskView.Items.Clear();
        ratePeak.Clear();

        base.OnUnload();
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Disk is not { } disk) {
            return;
        }

        int top      = diskView.TopIndex;
        int selected = diskView.SelectedIndex;

        diskView.Items.Clear();

        Theme theme      = appConfig.Theme;
        Color background = theme.ListViewBackground;
        Color foreground = theme.ListViewForeground;

        ratePeak.Add(disk.Metrics.Devices
            .Select(device => Math.Max(device.ReadBytesPerSecond, device.WriteBytesPerSecond))
            .DefaultIfEmpty(0)
            .Max());

        foreach (DiskDevice device in disk.Specs.Devices.OrderBy(device => device.Index)) {
            DiskDeviceMetrics? metrics = disk.Metrics.Devices.FirstOrDefault(candidate => candidate.Index == device.Index);

            double  activeRatio = metrics?.ToDiskActiveTimeRatio() ?? 0.0;
            double? celsius     = snapshot.Thermal?.Metrics.DiskTemperature(device.Index);

            ListViewItem row = new(
                [
                    string.Format(CultureInfo.CurrentCulture, Strings.SnapshotDiskLabelShort, device.Index),
                    snapshot.ToDiskName(device.Index.ToString()),
                    celsius.ToTemperatureText(),
                    metrics is null ? string.Empty : activeRatio.ToTextMetre(),
                    metrics?.ToDiskActiveTimePercentage() ?? Strings.CommonNotAvailable,
                    metrics is null ? string.Empty : ratePeak.Scale(metrics.ReadBytesPerSecond).ToTextMetre(),
                    metrics?.ToDiskReadRate() ?? Strings.CommonNotAvailable,
                    metrics is null ? string.Empty : ratePeak.Scale(metrics.WriteBytesPerSecond).ToTextMetre(),
                    metrics?.ToDiskWriteRate() ?? Strings.CommonNotAvailable
                ],
                background,
                foreground);

            row.SubItems[ActiveMetreColumn].ForegroundColor = activeRatio.ToTextMetreColour(theme);
            row.SubItems[ReadMetreColumn].ForegroundColor   = theme.StatusOk;
            row.SubItems[WriteMetreColumn].ForegroundColor  = theme.StatusOk;

            diskView.Items.Add(row);
        }

        if (diskView.Items.Count > 0) {
            diskView.TopIndex      = top;
            diskView.SelectedIndex = Math.Min(selected, diskView.Items.Count - 1);
            diskView.ScrollSelectedIntoView();
        }

        Draw();
    }
}
