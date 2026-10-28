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
    private const int    ChartHeight       = 9;

    private readonly AnsiScreenBuffer          frame      = new();
    private readonly Dictionary<string, Chart> chartCache = new();

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    
    private PowerControl? powerControl;
    private FansControl?  fansControl;

    private List<SensorRow> rows = new();
    private string lastSignature = string.Empty;
    
    private SystemSnapshot? snapshot;

    private int fanCount;
    private int scrollOffset;
    private int lastDrawnChartCount = -1;

    private bool needsFullClear = true;


    private sealed record SensorRow(
        string           Key, 
        string           Title, 
        ThermalComponent Component, 
        string           ComponentId);

    public ThermalsControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig) 
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;
    }

    private static string BuildChartText(SensorRow row, ThermalMetrics? metrics)
    {
        if (metrics?.PrimaryTemperature(row.Component, row.ComponentId) is not { } celsius) {
            return row.Title;
        }

        string text = $"{row.Title}   {celsius.ToTemperatureText()}";

        return row.Component == ThermalComponent.Cpu && metrics.Pressure is { } pressure
            ? $"{text}   Pressure: {pressure}"
            : text;
    }

    private string BuildTitle(ThermalComponent component, string componentId) => component switch {
        ThermalComponent.Cpu     => string.IsNullOrEmpty(componentId) ? "CPU" : $"CPU {componentId}",
        ThermalComponent.Gpu     => snapshot.ToGpuLabel(componentId),
        ThermalComponent.Disk    => snapshot.ToDiskLabel(componentId),
        ThermalComponent.Battery => "Battery",
        ThermalComponent.Chipset => "Chipset",
        ThermalComponent.Ambient => "Ambient",
        _ => "System"
    };

    internal IReadOnlyDictionary<string, Chart> Charts => chartCache;

    private int ChartsWidth => ShowPowerPane
        ? Math.Max(4, Width - PowerControl.PreferredWidth)
        : Math.Max(4, Width);

    private int ChartWidth => Math.Max(4, ChartsWidth - 1);

    private void ConfigureChart(Chart chart)
    {
        CommonControl.Theme(chart, appConfig.Theme);
        chart.MetreStyle     = appConfig.MetreStyle;
        chart.ShowGrid       = appConfig.ShowLargeMetreGrid;
        chart.ShowYAxisScale = appConfig.ShowYAxisScale;
    }

    private FansControl? CreateFansControl()
    {
#if __APPLE__
        return new FansControl(serviceController, Terminal, appConfig) { TabStop = false };
#else
        return null;
#endif
    }

    private void DrawEmptyState()
    {
#if __APPLE__
        string[] lines = [
            "No thermal sensors detected.",
            "The SMC and HID temperature sensors could not be read.",
        ];
#else
        string[] lines = [
            "No thermal sensors detected.",
            "GPU and NVMe drive temperatures appear here when available.",
            "Accurate CPU temperature needs a signed kernel helper driver.",
        ];
#endif

        for (int i = 0; i < lines.Length; i++) {
            string line = lines[i];
            int x = X + Math.Max(0, (ChartsWidth - line.Length) / 2);
            int y = Y + Height / 2 - lines.Length / 2 + i;

            frame.Clear();
            frame.MoveTo(x, y);
            frame.SetColour(appConfig.Theme.Foreground, BackgroundColour);
            frame.Append(line);
            
            Terminal.Write(frame.AsSpan());
        }
    }

    private void DrawGlyph(int x, int y, char glyph)
    {
        frame.Clear();
        frame.MoveTo(x, y);
        frame.SetColour(ScrollBarColour, BackgroundColour);
        frame.Append(glyph);
        frame.ResetColour();
        
        Terminal.Write(frame.AsSpan());
    }

    private void DrawScrollBar(int visibleCharts)
    {
        int x = X + ChartsWidth - 1;

        DrawVerticalLine(
            x, 
            Y, 
            Y + Height, 
            ScrollBarColour);

        if (scrollOffset > 0) {
            DrawGlyph(x, Y, '▲');
        }

        if (scrollOffset + visibleCharts < rows.Count) {
            DrawGlyph(x, Y + Height - 1, '▼');
        }
    }

    private void EnsureRows(ThermalMetrics metrics)
    {
        List<(ThermalComponent Component, string ComponentId)> groups = metrics.Sensors
            .Select(sensor => (sensor.Component, sensor.ComponentId))
            .Distinct()
            .OrderBy(group => group.Component)
            .ThenBy(group  => group.ComponentId, StringComparer.Ordinal)
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
            AutoScale                 = false,
            LabelSeries               = string.Empty,
            CustomYAxisScaleFormatter = value => $"{(int)Math.Round(value * FixedScaleCelsius)}°C",
        };

        ConfigureChart(chart);
        
        chart.Width  = ChartWidth;
        chart.Height = ChartHeight;
        chart.Resize();

        chartCache[key] = chart;
        
        return chart;
    }

    private void LayoutSidePanes()
    {
        int x          = X + ChartsWidth;
        int width      = Math.Max(1, Width - ChartsWidth);
        int fansHeight = fansControl is null ? 0 : Math.Min(FansControl.HeightFor(fanCount), Height);

        if (powerControl is not null) {
            powerControl.X      = x;
            powerControl.Y      = Y;
            powerControl.Width  = width;
            powerControl.Height = Height - fansHeight;
        }

        if (fansControl is not null) {
            fansControl.X      = x;
            fansControl.Y      = Y + Height - fansHeight;
            fansControl.Width  = width;
            fansControl.Height = fansHeight;
        }
    }

    protected override void OnDraw()
    {
        OnDrawInternal();
        powerControl?.Draw();
        fansControl?.Draw();
    }

    private void OnDrawInternal()
    {
        if (rows.Count == 0) {
            if (needsFullClear || lastDrawnChartCount != 0) {
                DrawRectangle(X, Y, ChartsWidth, Height, BackgroundColour);
            }

            DrawEmptyState();
            DrawScrollBar(visibleCharts: 1);

            needsFullClear      = false;
            lastDrawnChartCount = 0;
            return;
        }

        int visibleCharts = Math.Max(1, Height / ChartHeight);
        
        scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, rows.Count - visibleCharts));

        int drawn = Math.Min(visibleCharts, rows.Count - scrollOffset);

        if (needsFullClear) {
            DrawRectangle(
                X, 
                Y, 
                ChartsWidth, 
                Height, 
                BackgroundColour);
        }

        ThermalMetrics? metrics = snapshot?.Thermal?.Metrics;

        for (int i = 0; i < drawn; i++) {
            SensorRow row = rows[scrollOffset + i];

            if (!chartCache.TryGetValue(row.Key, out Chart? chart)) {
                continue;
            }

            chart.Text = BuildChartText(row, metrics);

            if (chart.Width != ChartWidth) {
                chart.Width = ChartWidth;
                chart.Resize();
            }

            chart.X      = X;
            chart.Y      = Y + i * ChartHeight;
            chart.Height = ChartHeight;
            chart.Draw();
        }

        int coveredRows = drawn * ChartHeight;

        if (!needsFullClear && drawn < lastDrawnChartCount && coveredRows < Height) {
            DrawRectangle(
                X, 
                Y + coveredRows, 
                ChartsWidth, 
                Height - coveredRows, 
                BackgroundColour);
        }

        DrawScrollBar(visibleCharts);

        needsFullClear = false;
        lastDrawnChartCount = drawn;
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        int visibleCharts = Math.Max(1, Height / ChartHeight);
        int maxOffset     = Math.Max(0, rows.Count - visibleCharts);

        switch (keyInfo.Key) {
            case ConsoleKey.UpArrow:
                scrollOffset = Math.Max(0, scrollOffset - 1);
                handled      = true;
                break;
            case ConsoleKey.DownArrow:
                scrollOffset = Math.Min(maxOffset, scrollOffset + 1);
                handled      = true;
                break;
            case ConsoleKey.PageUp:
                scrollOffset = Math.Max(0, scrollOffset - visibleCharts);
                handled      = true;
                break;
            case ConsoleKey.PageDown:
                scrollOffset = Math.Min(maxOffset, scrollOffset + visibleCharts);
                handled      = true;
                break;
            default:
                return;
        }

        Draw();
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);

        if (ShowPowerPane && powerControl is null) {
            powerControl = new PowerControl(serviceController, Terminal, appConfig) { TabStop = false };
            Controls.Add(powerControl);
        }

        if (ShowPowerPane && fansControl is null && CreateFansControl() is { } fans) {
            fansControl = fans;
            Controls.Add(fansControl);
        }

        foreach (Chart chart in chartCache.Values) {
            ConfigureChart(chart);
        }

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        base.OnLoad();
    }

    protected override void OnResize()
    {
        foreach (Chart chart in chartCache.Values) {
            chart.X      = X;
            chart.Width  = ChartWidth;
            chart.Height = ChartHeight;
            chart.Resize();
        }

        LayoutSidePanes();

        needsFullClear      = true;
        lastDrawnChartCount = -1;

        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        scrollOffset        = 0;
        needsFullClear      = true;
        lastDrawnChartCount = -1;

        base.OnUnload();
    }

    private void PruneChartCache()
    {
        HashSet<string> live = rows.Select(row => row.Key).ToHashSet();

        foreach (string stale in chartCache.Keys.Where(key => !live.Contains(key)).ToList()) {
            chartCache.Remove(stale);
        }
    }

    public void Sample(SystemSnapshot snapshot)
    {
        this.snapshot = snapshot;

        if (snapshot.Thermal is { } thermal) {
            EnsureRows(thermal.Metrics);
            FeedCharts(thermal.Metrics);
        }

        if (fansControl is not null && snapshot.Thermal?.Metrics.Fans.Count is { } count && count != fanCount) {
            fanCount = count;

            LayoutSidePanes();
            powerControl?.Resize();
            fansControl.Resize();
        }

        Draw();
    }

    internal Color ScrollBarColour =>
        Focused
            ? FocusSelectionColour
            : appConfig.Theme.ChartBorderForeground;

    public bool ShowPowerPane { get; init; } = true;
}
