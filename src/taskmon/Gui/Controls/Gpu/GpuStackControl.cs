using System.Drawing;
using System.Globalization;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.Gui.Controls.Gpu;

public sealed class GpuStackControl : Control
{
    private const int IdColumnWidth          = 7;
    private const int TemperatureColumnWidth = 7;
    private const int PowerColumnWidth       = 10;
    private const int MetreColumnWidth       = TextMetreExtensions.TextMetreCells + 1;
    private const int PercentColumnWidth     = 7;
    private const int MinNameWidth           = 12;
    private const int BorderAndGutters       = 3;

    private const int GpuMetreColumn    = 4;
    private const int MemoryMetreColumn = 6;

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly ListView          gpuView;

    public GpuStackControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        gpuView = new ListView(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = false,
            Visible           = true,
            HeaderText        = Strings.GpuStackHeader,
            EmptyListViewText = Strings.GpuStackNoGpus
        };

        gpuView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.GpuStackColumnId))
            .Add(new ListViewColumnHeader(Strings.GpuStackColumnName))
            .Add(new ListViewColumnHeader(Strings.GpuStackColumnTemperature))
            .Add(new ListViewColumnHeader(Strings.GpuStackColumnPower))
            .Add(new ListViewColumnHeader(Strings.GpuStackColumnGpu))
            .Add(new ListViewColumnHeader(Strings.GpuStackColumnGpuPercent))
            .Add(new ListViewColumnHeader(Strings.GpuStackColumnMemory))
            .Add(new ListViewColumnHeader(Strings.GpuStackColumnMemoryPercent));

        Controls.Add(gpuView);
    }

    internal static int[] ComputeColumnWidths(int width)
    {
        int available       = Math.Max(0, width - BorderAndGutters);
        int fixedWithMetres = IdColumnWidth + TemperatureColumnWidth + PowerColumnWidth + 2 * (MetreColumnWidth + PercentColumnWidth);
        int metre           = available - fixedWithMetres >= MinNameWidth ? MetreColumnWidth : 0;
        int fixedWidth      = IdColumnWidth + TemperatureColumnWidth + PowerColumnWidth + 2 * (metre + PercentColumnWidth);
        int name            = Math.Max(0, available - fixedWidth);

        return [IdColumnWidth, name, TemperatureColumnWidth, PowerColumnWidth, metre, PercentColumnWidth, metre, PercentColumnWidth];
    }

    internal ListView GpuView => gpuView;

    protected override void OnDraw()
    {
        gpuView.Focused = Focused;
        gpuView.Draw();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled) =>
        gpuView.KeyPressed(keyInfo, ref handled);

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(gpuView, appConfig.Theme);

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        base.OnLoad();
    }

    protected override void OnResize()
    {
        gpuView.X      = X;
        gpuView.Y      = Y;
        gpuView.Width  = Width;
        gpuView.Height = Height;

        int[] widths = ComputeColumnWidths(Width);

        for (int i = 0; i < widths.Length; i++) {
            gpuView.ColumnHeaders[i].Width = widths[i];
        }

        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        gpuView.Items.Clear();

        base.OnUnload();
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Gpu is not { } gpu) {
            return;
        }

        int top      = gpuView.TopIndex;
        int selected = gpuView.SelectedIndex;

        gpuView.Items.Clear();

        Theme theme      = appConfig.Theme;
        Color background = theme.ListViewBackground;
        Color foreground = theme.ListViewForeground;

        foreach (GpuDevice device in gpu.Specs.Devices.OrderBy(device => device.Index)) {
            GpuDeviceMetrics? metrics = gpu.Metrics.Devices.FirstOrDefault(candidate => candidate.AdapterLuid == device.AdapterLuid);

            double  gpuRatio    = metrics?.GpuPercentTime ?? 0.0;
            double  memoryRatio = metrics?.ToGpuMemoryRatio() ?? 0.0;
            double? celsius     = snapshot.Thermal?.Metrics.GpuTemperature(device.AdapterLuid);
            double? watts       = snapshot.Power?.Metrics.GpuPower(device.AdapterLuid);

            ListViewItem row = new(
                [
                    string.Format(CultureInfo.CurrentCulture, Strings.SnapshotGpuNameShort, device.Index),
                    snapshot.ToGpuName(device.AdapterLuid.ToString()),
                    celsius.ToTemperatureText(),
                    watts.ToWattText(),
                    metrics is null ? string.Empty : gpuRatio.ToTextMetre(),
                    metrics?.ToGpuPercentage() ?? Strings.CommonNotAvailable,
                    metrics is null ? string.Empty : memoryRatio.ToTextMetre(),
                    metrics?.ToGpuMemoryPercentage() ?? Strings.CommonNotAvailable
                ],
                background,
                foreground);

            row.SubItems[GpuMetreColumn].ForegroundColor    = gpuRatio.ToTextMetreColour(theme);
            row.SubItems[MemoryMetreColumn].ForegroundColor = memoryRatio.ToTextMetreColour(theme);

            gpuView.Items.Add(row);
        }

        if (gpuView.Items.Count > 0) {
            gpuView.TopIndex      = top;
            gpuView.SelectedIndex = Math.Min(selected, gpuView.Items.Count - 1);
            gpuView.ScrollSelectedIntoView();
        }

        Draw();
    }
}
