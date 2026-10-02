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

    private bool needsFullClear = true;
    private int lastDrawnChartCount = -1;
    private bool scrollBarShown;

    private SystemSnapshot? snapshot;

    private sealed record SensorRow(
        string Key, 
        string Title, 
        ThermalComponent Component, 
        string ComponentId);

    public ThermalsControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;
    }

    public void Sample(SystemSnapshot snapshot)
    {
        this.snapshot = snapshot;

        if (snapshot.Thermal is { } thermal) {
            EnsureRows(thermal.Metrics);
            FeedCharts(thermal.Metrics);
        }

        Draw();
    }

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
        needsFullClear = true;
    }

    private string BuildTitle(ThermalComponent component, string componentId) => component switch {
        ThermalComponent.Cpu     => "CPU",
        ThermalComponent.Gpu     => GpuLabel(componentId),
        ThermalComponent.Disk    => DiskLabel(componentId),
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
        chart.BackgroundColour = appConfig.Theme.ChartBackground;
        chart.ForegroundColour = appConfig.Theme.Foreground;
        chart.BorderForegroundColour = appConfig.Theme.ChartBorderForeground;
        chart.BorderBackgroundColour = appConfig.Theme.ChartBorderBackground;
        chart.ColourHigh = appConfig.Theme.RangeHighBackground;
        chart.ColourLow = appConfig.Theme.RangeLowBackground;
        chart.ColourMid = appConfig.Theme.RangeMidBackground;
        chart.MetreStyle = appConfig.MetreStyle;
        chart.YAxisColour = appConfig.Theme.ChartYAxis;
    }

    protected override void OnDraw() => OnDrawInternal();

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

        bool scrollNeeded = rows.Count > visibleCharts;
        int chartWidth = scrollNeeded ? Math.Max(4, Width - 1) : Width;

        if (scrollNeeded != scrollBarShown) {
            needsFullClear = true;
            scrollBarShown = scrollNeeded;
        }

        if (needsFullClear) {
            DrawRectangle(
                X, 
                Y, 
                Width, 
                Height, 
                BackgroundColour);
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

        int coveredRows = drawn * ChartHeight;

        if (!needsFullClear && drawn < lastDrawnChartCount && coveredRows < Height) {
            DrawRectangle(
                X, 
                Y + coveredRows, 
                Width, 
                Height - coveredRows, 
                BackgroundColour);
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
            frame.SetColour(appConfig.Theme.Foreground, BackgroundColour);
            frame.Append(line);
            
            Terminal.Write(frame.AsSpan());
        }
    }

    private void DrawScrollBar(int visibleCharts)
    {
        int x = X + Width - 1;

        DrawVerticalLine(
            x, 
            Y, 
            Y + Height, 
            appConfig.Theme.ChartBorderForeground);

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
        frame.SetColour(appConfig.Theme.ChartBorderForeground, BackgroundColour);
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
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

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
