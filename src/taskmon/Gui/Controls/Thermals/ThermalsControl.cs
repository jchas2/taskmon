using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Gui.Controls.Thermals;

// Every temperature the machine reports, one chart per sensor, stacked and scrollable. Each chart
// keeps its own gap-free history and is drawn on a fixed 0-110 C scale so sensors stay visually
// comparable. CPU coverage depends on ACPI thermal zones and is often absent - see the empty state.
public sealed class ThermalsControl : Control
{
    private const double FixedScaleCelsius = 110.0;
    private const int ChartHeight = 9;

    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly AnsiScreenBuffer frame = new();

    private readonly Dictionary<string, Chart> chartCache = new();
    private List<SensorRow> rows = new();
    private string lastSignature = string.Empty;
    private int scrollOffset;

    // The charts repaint their own cells in place every frame, so the control is only wiped when
    // the layout actually changes - a sensor added or removed, a resize, a fresh activation. A
    // per-frame full clear is what makes charts flicker.
    private bool needsFullClear = true;
    private int lastDrawnChartCount = -1;

    // Whether the last draw reserved the right-hand column for a scroll bar. A change flips the
    // chart width, so it forces a one-off wipe.
    private bool scrollBarShown;

    private SystemSnapshot? snapshot;

    // One row - and one chart - per hardware component, not per sensor: a drive that reports a
    // composite plus a controller sensor is still a single "Disk 0", matching the performance
    // panels. The chart plots that component's headline temperature (see PrimaryTemperature).
    private sealed record SensorRow(string Key, string Title, ThermalComponent Component, string ComponentId);

    public ThermalsControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;
    }

    // Wired to the controller event in OnLoad; tests call it directly.
    public void Sample(SystemSnapshot snapshot)
    {
        this.snapshot = snapshot;

        if (snapshot.Thermal is { } thermal) {
            EnsureRows(thermal.Metrics);
            FeedCharts(thermal.Metrics);
        }

        Draw();
    }

    // ---- Rows / charts ---------------------------------------------------------------------------

    private void EnsureRows(ThermalMetrics metrics)
    {
        List<(ThermalComponent Component, string ComponentId)> groups = metrics.Sensors
            .Select(sensor => (sensor.Component, sensor.ComponentId))
            .Distinct()
            .OrderBy(group => group.Component)
            .ThenBy(group => group.ComponentId, StringComparer.Ordinal)
            .ToList();

        string signature = string.Join("|", groups.Select(group => $"{group.Component}:{group.ComponentId}"));

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;

        rows = groups
            .Select(group => new SensorRow(
                $"{group.Component}:{group.ComponentId}",
                BuildTitle(group.Component, group.ComponentId),
                group.Component,
                group.ComponentId))
            .ToList();

        PruneChartCache();
        scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, rows.Count - 1));

        // The set of charts changed - drop any residue from charts that are gone or have moved.
        needsFullClear = true;
    }

    private string BuildTitle(ThermalComponent component, string componentId) => component switch {
        ThermalComponent.Cpu => "CPU",
        ThermalComponent.Gpu => GpuLabel(componentId),
        ThermalComponent.Disk => DiskLabel(componentId),
        ThermalComponent.Battery => "Battery",
        ThermalComponent.Chipset => "Chipset",
        ThermalComponent.Ambient => "Ambient",
        _ => "System"
    };

    private string GpuLabel(string componentId)
    {
        if (long.TryParse(componentId, out long luid) &&
            snapshot?.Gpu?.Specs.Devices.FirstOrDefault(device => device.AdapterLuid == luid) is { } gpu) {

            return $"GPU · {gpu.Description}";
        }

        return "GPU";
    }

    private string DiskLabel(string componentId)
    {
        if (int.TryParse(componentId, out int index) &&
            snapshot?.Disk?.Specs.Devices.FirstOrDefault(device => device.Index == index) is { } disk) {

            return $"Disk {index} · {disk.Model}";
        }

        return $"Disk {componentId}";
    }

    private void FeedCharts(ThermalMetrics metrics)
    {
        foreach (SensorRow row in rows) {
            if (metrics.PrimaryTemperature(row.Component, row.ComponentId) is { } celsius) {
                GetOrCreateChart(row.Key).AddData(celsius / FixedScaleCelsius);
            }
        }
    }

    private Chart GetOrCreateChart(string key)
    {
        if (chartCache.TryGetValue(key, out Chart? existing)) {
            return existing;
        }

        Chart chart = new(Terminal) {
            AutoScale = false,
            LabelSeries = string.Empty,
            ShowGrid = true,
            ShowYAxisScale = true,
            CustomYAxisScaleFormatter = value => $"{(int)Math.Round(value * FixedScaleCelsius)}°C",
        };

        ConfigureChart(chart);
        chart.Width = Math.Max(4, Width);
        chart.Height = ChartHeight;
        chart.Resize();

        chartCache[key] = chart;
        return chart;
    }

    private void PruneChartCache()
    {
        HashSet<string> live = rows.Select(row => row.Key).ToHashSet();

        foreach (string stale in chartCache.Keys.Where(key => !live.Contains(key)).ToList()) {
            chartCache.Remove(stale);
        }
    }

    private void ConfigureChart(Chart chart)
    {
        chart.BackgroundColour = appConfig.DefaultTheme.Background;
        chart.ForegroundColour = appConfig.DefaultTheme.Foreground;
        chart.BorderColour = appConfig.DefaultTheme.ChartBorder;
        chart.ColourHigh = appConfig.DefaultTheme.RangeHighBackground;
        chart.ColourLow = appConfig.DefaultTheme.RangeLowBackground;
        chart.ColourMid = appConfig.DefaultTheme.RangeMidBackground;
        chart.MetreStyle = appConfig.MetreStyle;
        chart.YAxisColour = appConfig.DefaultTheme.ChartYAxis;
    }

    // ---- Draw / input / lifecycle -------------------------------------------------------------

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();
            OnDrawInternal();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    private void OnDrawInternal()
    {
        if (rows.Count == 0) {
            if (needsFullClear || lastDrawnChartCount != 0 || scrollBarShown) {
                DrawRectangle(X, Y, Width, Height, BackgroundColour);
            }

            DrawEmptyState();

            needsFullClear = false;
            lastDrawnChartCount = 0;
            scrollBarShown = false;
            return;
        }

        int visibleCharts = Math.Max(1, Height / ChartHeight);
        scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, rows.Count - visibleCharts));

        int drawn = Math.Min(visibleCharts, rows.Count - scrollOffset);

        // When there is more than one screenful, reserve the right-hand column for a scroll bar and
        // give the charts one column less; otherwise the charts fill the whole width.
        bool scrollNeeded = rows.Count > visibleCharts;
        int chartWidth = scrollNeeded ? Math.Max(4, Width - 1) : Width;

        if (scrollNeeded != scrollBarShown) {
            needsFullClear = true;
            scrollBarShown = scrollNeeded;
        }

        // A one-off wipe when the layout changed. In steady state nothing is cleared: each chart's
        // Draw() overwrites every cell it owns, so repainting in place does not flicker.
        if (needsFullClear) {
            DrawRectangle(X, Y, Width, Height, BackgroundColour);
        }

        ThermalMetrics? metrics = snapshot?.Thermal?.Metrics;

        for (int i = 0; i < drawn; i++) {
            SensorRow row = rows[scrollOffset + i];

            if (!chartCache.TryGetValue(row.Key, out Chart? chart)) {
                continue;
            }

            double? current = metrics?.PrimaryTemperature(row.Component, row.ComponentId);

            chart.Text = current is { } value
                ? $"{row.Title}   {value.ToTemperatureText()}"
                : row.Title;

            if (chart.Width != chartWidth) {
                chart.Width = chartWidth;
                chart.Resize();
            }

            chart.X = X;
            chart.Y = Y + i * ChartHeight;
            chart.Height = ChartHeight;
            chart.Draw();
        }

        // Clear the strip below the last chart only when fewer charts are on screen than last time
        // (scrolled to the end, or a sensor went away). It never overlaps a chart, so no flicker.
        int coveredRows = drawn * ChartHeight;

        if (!needsFullClear && drawn < lastDrawnChartCount && coveredRows < Height) {
            DrawRectangle(X, Y + coveredRows, Width, Height - coveredRows, BackgroundColour);
        }

        if (scrollNeeded) {
            DrawScrollBar(visibleCharts);
        }

        needsFullClear = false;
        lastDrawnChartCount = drawn;
    }

    private void DrawEmptyState()
    {
        string[] lines = [
            "No thermal sensors detected.",
            "GPU and NVMe drive temperatures appear here when available.",
            "Accurate CPU temperature needs a signed kernel helper driver and is not read.",
        ];

        for (int i = 0; i < lines.Length; i++) {
            string line = lines[i];
            int x = X + Math.Max(0, (Width - line.Length) / 2);
            int y = Y + Height / 2 - lines.Length / 2 + i;

            frame.Clear();
            frame.MoveTo(x, y);
            frame.SetColour(appConfig.DefaultTheme.Foreground, BackgroundColour);
            frame.Append(line);
            Terminal.Write(frame.AsSpan());
        }
    }

    // The reserved right-hand column: a full-height rule with a ▲ / ▼ at the ends to show which
    // way there is more to scroll. The rule's own rounded caps stand in when an arrow is absent
    // (nothing above / nothing below).
    private void DrawScrollBar(int visibleCharts)
    {
        int x = X + Width - 1;

        DrawVerticalLine(x, Y, Y + Height, appConfig.DefaultTheme.ChartBorder);

        if (scrollOffset > 0) {
            DrawGlyph(x, Y, '▲');
        }

        if (scrollOffset + visibleCharts < rows.Count) {
            DrawGlyph(x, Y + Height - 1, '▼');
        }
    }

    private void DrawGlyph(int x, int y, char glyph)
    {
        frame.Clear();
        frame.MoveTo(x, y);
        frame.SetColour(appConfig.DefaultTheme.ChartBorder, BackgroundColour);
        frame.Append(glyph);
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        int visibleCharts = Math.Max(1, Height / ChartHeight);
        int maxOffset = Math.Max(0, rows.Count - visibleCharts);

        switch (keyInfo.Key) {
            case ConsoleKey.UpArrow:
                scrollOffset = Math.Max(0, scrollOffset - 1);
                handled = true;
                break;
            case ConsoleKey.DownArrow:
                scrollOffset = Math.Min(maxOffset, scrollOffset + 1);
                handled = true;
                break;
            case ConsoleKey.PageUp:
                scrollOffset = Math.Max(0, scrollOffset - visibleCharts);
                handled = true;
                break;
            case ConsoleKey.PageDown:
                scrollOffset = Math.Min(maxOffset, scrollOffset + visibleCharts);
                handled = true;
                break;
            default:
                return;
        }

        Draw();
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        foreach (Chart chart in chartCache.Values) {
            ConfigureChart(chart);
        }

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        base.OnLoad();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnResize()
    {
        foreach (Chart chart in chartCache.Values) {
            chart.X = X;
            chart.Width = Math.Max(4, Width);
            chart.Height = ChartHeight;
            chart.Resize();
        }

        // The geometry changed - the next draw wipes once before repainting.
        needsFullClear = true;
        lastDrawnChartCount = -1;

        base.OnResize();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        chartCache.Clear();
        rows = new();
        lastSignature = string.Empty;
        scrollOffset = 0;
        needsFullClear = true;
        lastDrawnChartCount = -1;
        scrollBarShown = false;

        base.OnUnload();
    }
}
