using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Network;

namespace Task.Monitor.Gui.Controls.Performance;

public sealed class PerformanceControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;

    // One detail pane and one mini-chart per panel key, so nothing is shared between device
    // panels. Both survive a rebuild if their key does, keeping their history.
    private readonly Dictionary<string, Chart> chartCache = new();
    private readonly Dictionary<string, Control> detailCache = new();

    private List<PerformancePanelControl> panelControls;
    private PerformancePanelControl activeControl;
    private bool loaded;

    // Every panel is a fixed height; the panel column is a viewport that scrolls a whole panel
    // at a time when the list is taller than the control.
    private const int PanelHeight = 7;

    private readonly AnsiScreenBuffer scrollFrame = new();
    private int scrollOffset = 0;
    private int panelColumnWidth = 0;
    private int visiblePanelCount = 0;

    public PerformanceControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;

        panelControls = BuildPanels(snapshot: null);
        AttachPanels();

        activeControl = panelControls[0];
        activeControl.IsSelected = true;
    }

    // ---- Panel construction ------------------------------------------------------------------

    private List<PerformancePanelControl> BuildPanels(SystemSnapshot? snapshot)
    {
        List<PerformancePanelControl> panels = new();

        panels.Add(MakePanel(
            "cpu", $"CPU", autoScaleChart: false, Chart.FormatYScalePercentage,
            (panel, s) => {
                if (s.Cpu is not { } cpu) {
                    return;
                }

                panel.Line1 = string.Empty;
                panel.Line2 = $"{cpu.Metrics.ToCpuPercentage()} {cpu.Specs.ToCpuFrequencyGhz()}";
                panel.Line3 = $"{cpu.Specs.CpuCores} logical processors";
                panel.Line4 = string.Empty;
                panel.Chart.Add(cpu.Metrics.CpuTotalTime);
            }));

        panels.Add(MakePanel(
            "memory", "MEMORY", autoScaleChart: false, Chart.FormatYScalePercentage,
            (panel, s) => {
                if (s.Memory is not { } memory) {
                    return;
                }

                panel.Line1 = string.Empty;
                panel.Line2 = memory.Metrics.ToMemoryRatioFormattedBytes();
                panel.Line3 = memory.Metrics.ToMemoryPercentage();
                panel.Line4 = string.Empty;
                panel.Chart.Add(memory.Metrics.AvailablePhysicalRatio);
            }));

        AddGpuPanels(panels, snapshot);
        AddDiskPanels(panels, snapshot);
        AddNetworkPanels(panels, snapshot);

        return panels;
    }

    private void AddGpuPanels(List<PerformancePanelControl> panels, SystemSnapshot? snapshot)
    {
        List<GpuDeviceMetrics> devices = snapshot?.Gpu?.Metrics.Devices ?? new();

        if (devices.Count == 0) {
            panels.Add(MakePanel(
                "gpu", "GPU", autoScaleChart: false, Chart.FormatYScalePercentage,
                (panel, s) => {
                    if (s.Gpu is not { } gpu) {
                        return;
                    }

                    panel.Line1 = string.Empty;
                    panel.Line2 = gpu.Metrics.ToGpuPercentage();
                    panel.Line3 = $"{gpu.Specs.GpuCores} cores";
                    panel.Line4 = string.Empty;
                    panel.Chart.Add(gpu.Metrics.GpuPercentTime);
                }));

            return;
        }

        foreach (GpuDeviceMetrics device in devices) {
            long luid = device.AdapterLuid;
            GpuDevice? spec = snapshot?.Gpu?.Specs.Devices.FirstOrDefault(candidate => candidate.AdapterLuid == luid);
            string title = spec?.ToDisplayName() ?? $"GPU {device.Index}";

            panels.Add(MakePanel(
                $"gpu:{luid}", title, autoScaleChart: false, Chart.FormatYScalePercentage,
                (panel, s) => {
                    GpuDeviceMetrics? match = s.Gpu?.Metrics.Devices.FirstOrDefault(candidate => candidate.AdapterLuid == luid);

                    if (match == null) {
                        return;
                    }

                    panel.Line1 = string.Empty;
                    panel.Line2 = AppendTemperature(match.ToGpuPercentage(), s.Thermal?.Metrics.GpuTemperature(luid));
                    panel.Line3 = match.ToGpuMemoryRatioFormattedBytes();
                    panel.Line4 = string.Empty;
                    panel.Chart.Add(match.GpuPercentTime);
                }));
        }
    }

    private void AddDiskPanels(List<PerformancePanelControl> panels, SystemSnapshot? snapshot)
    {
        List<DiskDeviceMetrics> devices = snapshot?.Disk?.Metrics.Devices ?? new();

        if (devices.Count == 0) {
            panels.Add(MakePanel(
                "disk", "DISKS", autoScaleChart: false, Chart.FormatYScalePercentage,
                (panel, s) => {
                    if (s.Disk is not { } disk) {
                        return;
                    }

                    panel.Line1 = string.Empty;
                    panel.Line2 = disk.Metrics.ToDiskActiveTimePercentage();
                    panel.Line3 = disk.Metrics.ToDiskTransferRate();
                    panel.Line4 = string.Empty;
                    panel.Chart.Add(disk.Metrics.ToDiskActiveTimeRatio());
                }));

            return;
        }

        foreach (DiskDeviceMetrics device in devices) {
            int index = device.Index;
            DiskDevice? spec = snapshot?.Disk?.Specs.Devices.FirstOrDefault(candidate => candidate.Index == index);
            string title = spec?.ToDisplayName() ?? $"Disk {index}";

            panels.Add(MakePanel(
                $"disk:{index}", title, autoScaleChart: false, Chart.FormatYScalePercentage,
                (panel, s) => {
                    DiskDeviceMetrics? match = s.Disk?.Metrics.Devices.FirstOrDefault(candidate => candidate.Index == index);

                    if (match == null) {
                        return;
                    }

                    panel.Line1 = string.Empty;
                    panel.Line2 = AppendTemperature(
                        match.ToDiskActiveTimePercentage(), s.Thermal?.Metrics.DiskTemperature(index));
                    panel.Line3 = match.ToDiskTransferRate();
                    panel.Line4 = string.Empty;
                    panel.Chart.Add(match.ToDiskActiveTimeRatio());
                }));
        }
    }

    private void AddNetworkPanels(List<PerformancePanelControl> panels, SystemSnapshot? snapshot)
    {
        List<NetworkDeviceMetrics> devices = snapshot?.Network?.Metrics.Devices ?? new();

        if (devices.Count == 0) {
            panels.Add(MakePanel(
                "net", "NETWORK", autoScaleChart: true, Chart.FormatYScaleCompact,
                (panel, s) => {
                    if (s.Network is not { } network) {
                        return;
                    }

                    panel.Line1 = string.Empty;
                    panel.Line2 = $"S: {network.Metrics.ToNetworkSendRate()}";
                    panel.Line3 = $"R: {network.Metrics.ToNetworkReceiveRate()}";
                    panel.Line4 = string.Empty;
                    panel.Chart.Add(network.Metrics.ToNetworkThroughputBytesPerSecond());
                }));

            return;
        }

        foreach (NetworkDeviceMetrics device in devices) {
            ulong luid = device.InterfaceLuid;
            NetworkDevice? spec = snapshot?.Network?.Specs.Devices.FirstOrDefault(candidate => candidate.InterfaceLuid == luid);
            string title = spec?.ToDisplayName() ?? device.ToDisplayName();

            panels.Add(MakePanel(
                $"net:{luid}", title, autoScaleChart: true, Chart.FormatYScaleCompact,
                (panel, s) => {
                    NetworkDeviceMetrics? match = s.Network?.Metrics.Devices.FirstOrDefault(candidate => candidate.InterfaceLuid == luid);

                    if (match == null) {
                        return;
                    }

                    panel.Line1 = string.Empty;
                    panel.Line2 = $"S: {match.ToNetworkSendRate()}";
                    panel.Line3 = $"R: {match.ToNetworkReceiveRate()}";
                    panel.Line4 = string.Empty;
                    panel.Chart.Add(match.ToNetworkThroughputBytesPerSecond());
                }));
        }
    }

    // Adds a "  ·  61°C" suffix when a temperature is available, leaving the line untouched
    // otherwise so machines with no thermal sensors read exactly as before.
    private static string AppendTemperature(string line, double? celsius) =>
        celsius is { } value ? $"{line} ({value.ToTemperatureText()})" : line;

    private PerformancePanelControl MakePanel(
        string key,
        string title,
        bool autoScaleChart,
        Func<double, string> chartFormatter,
        Action<PerformancePanelControl, SystemSnapshot> bind)
    {
        Chart chart = GetOrCreateChart(key, autoScaleChart, chartFormatter);
        Control detail = GetOrCreateDetail(key);

        PerformancePanelControl panel = new(
            Terminal, 
            chart, 
            detail, 
            key, 
            title, 
            appConfig);

        panel.Bind = snapshot => bind(panel, snapshot);

        ConfigureColours(panel);

        return panel;
    }

    private Chart GetOrCreateChart(string key, bool autoScale, Func<double, string> formatter)
    {
        if (chartCache.TryGetValue(key, out Chart? chart)) {
            chart.AutoScale = autoScale;
            chart.CustomYAxisScaleFormatter = formatter;
            return chart;
        }

        chart = new Chart(Terminal) {
            Text = string.Empty,
            LabelSeries = string.Empty,
            ShowGrid = false,
            ShowYAxisScale = false,
            AutoScale = autoScale,
            CustomYAxisScaleFormatter = formatter,
        };

        ConfigureChart(chart);
        chartCache[key] = chart;

        return chart;
    }

    private Control GetOrCreateDetail(string key)
    {
        if (detailCache.TryGetValue(key, out Control? detail)) {
            return detail;
        }

        detail = CreateDetail(key);
        ConfigureColours(detail);
        Controls.Add(detail);

        if (loaded) {
            detail.Load();
        }

        detailCache[key] = detail;

        return detail;
    }

    private Control CreateDetail(string key)
    {
        if (key == "cpu") {
            return new CpuPerformanceControl(Terminal, appConfig);
        }

        if (key == "memory") {
            return new MemoryPerformanceControl(Terminal, appConfig);
        }

        int separator = key.IndexOf(':');
        string prefix = separator < 0 ? key : key[..separator];
        string? id = separator < 0 ? null : key[(separator + 1)..];

        switch (prefix) {
            case "gpu": {
                GpuPerformanceControl control = new(Terminal, appConfig);
                control.SetScope(id != null && long.TryParse(id, out long luid) 
                    ? luid 
                    : null);
                return control;
            }

            case "disk": {
                DiskPerformanceControl control = new(Terminal, appConfig);
                control.SetScope(id != null && int.TryParse(id, out int index) 
                    ? index 
                    : null);
                return control;
            }

            case "net": {
                NetworkPerformanceControl control = new(Terminal, appConfig);
                control.SetScope(id != null && ulong.TryParse(id, out ulong luid) 
                    ? luid 
                    : null);
                return control;
            }

            default:
                throw new InvalidOperationException($"Unknown performance panel key '{key}'.");
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
        chart.ShowYAxisScale = appConfig.ShowYAxisScale;
        chart.YAxisColour = appConfig.DefaultTheme.ChartYAxis;
    }

    private void ConfigureColours(Control control)
    {
        control.BackgroundColour = appConfig.DefaultTheme.Background;
        control.ForegroundColour = appConfig.DefaultTheme.Foreground;
    }

    private void AttachPanels()
    {
        foreach (PerformancePanelControl panel in panelControls) {
            Controls.Add(panel);
        }
    }

    private void DetachPanels()
    {
        foreach (PerformancePanelControl panel in panelControls) {
            Controls.Remove(panel);
        }
    }

    private void PruneCaches()
    {
        HashSet<string> live = panelControls.Select(panel => panel.Key).ToHashSet();

        foreach (string stale in chartCache.Keys.Where(key => !live.Contains(key)).ToList()) {
            chartCache.Remove(stale);
        }

        foreach (string stale in detailCache.Keys.Where(key => !live.Contains(key)).ToList()) {
            Control detail = detailCache[stale];
            detail.Unload();
            Controls.Remove(detail);
            detailCache.Remove(stale);
        }
    }

    // ---- Draw / input / lifecycle ----------------------------------------------------------

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
        foreach (PerformancePanelControl panel in panelControls) {
            panel.Draw();
        }

        foreach (PerformancePanelControl panel in panelControls) {
            panel.Chart.Draw();
        }

        DrawVerticalLine(
            X + panelColumnWidth,
            Y,
            Y + Height,
            appConfig.DefaultTheme.ChartBorder);

        DrawPanelColumnFiller();
        DrawScrollIndicators();

        activeControl.AssociatedControl.Draw();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        int index;

        switch (keyInfo.Key) {
            case ConsoleKey.UpArrow:
                index = panelControls.IndexOf(activeControl);
                if (index <= 0) {
                    break;
                }
                SetActiveControl(panelControls[index - 1]);
                handled = true;
                break;

            case ConsoleKey.DownArrow:
                index = panelControls.IndexOf(activeControl);
                if (index < 0 || index == panelControls.Count - 1) {
                    break;
                }
                SetActiveControl(panelControls[index + 1]);
                handled = true;
                break;
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        foreach (Control ctrl in Controls) {
            ctrl.BackgroundColour = appConfig.DefaultTheme.Background;
            ctrl.ForegroundColour = appConfig.DefaultTheme.Foreground;
        }

        foreach (PerformancePanelControl panel in panelControls) {
            ConfigureChart(panel.Chart);
            panel.Visible = true;
            panel.IsSelected = false;
            panel.Load();
        }

        foreach (Control detail in detailCache.Values) {
            detail.Load();
        }

        loaded = true;

        activeControl = panelControls[0];
        activeControl.IsSelected = true;

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        SystemSnapshot snapshot = e.Snapshot;
        RebuildPanelsIfNeeded(snapshot);

        foreach (Control detail in detailCache.Values) {
            ((IPerformanceDetail)detail).Sample(snapshot);
        }

        foreach (PerformancePanelControl panel in panelControls) {
            panel.Update(snapshot);
        }

        Draw();
    }

    private List<string> DesiredPanelKeys(SystemSnapshot? snapshot)
    {
        List<string> keys = new() { "cpu", "memory" };

        AppendDeviceKeys(keys, "gpu",
            snapshot?.Gpu?.Metrics.Devices.Select(device => device.AdapterLuid.ToString()));

        AppendDeviceKeys(keys, "disk",
            snapshot?.Disk?.Metrics.Devices.Select(device => device.Index.ToString()));

        AppendDeviceKeys(keys, "net",
            snapshot?.Network?.Metrics.Devices.Select(device => device.InterfaceLuid.ToString()));

        return keys;
    }

    internal static void AppendDeviceKeys(
        List<string> keys,
        string prefix,
        IEnumerable<string>? deviceIds)
    {
        List<string> ids = deviceIds?.ToList() ?? new();

        if (ids.Count == 0) {
            keys.Add(prefix);
            return;
        }

        foreach (string id in ids) {
            keys.Add($"{prefix}:{id}");
        }
    }

    private void RebuildPanelsIfNeeded(SystemSnapshot snapshot)
    {
        if (panelControls.Select(panel => panel.Key).SequenceEqual(DesiredPanelKeys(snapshot))) {
            return;
        }

        RebuildPanels(snapshot);
    }

    private void RebuildPanels(SystemSnapshot snapshot)
    {
        string previousKey = activeControl.Key;

        activeControl.IsSelected = false;

        DetachPanels();
        panelControls = BuildPanels(snapshot);
        AttachPanels();
        PruneCaches();

        foreach (PerformancePanelControl panel in panelControls) {
            ConfigureChart(panel.Chart);
            panel.Visible = true;
            panel.IsSelected = false;
            panel.Load();
        }

        activeControl = panelControls.FirstOrDefault(panel => panel.Key == previousKey)
            ?? panelControls.FirstOrDefault(panel => SamePrefix(panel.Key, previousKey))
            ?? panelControls[0];

        scrollOffset = 0;
        activeControl.IsSelected = true;

        DrawRectangle(
            X, 
            Y, 
            Width, 
            Height, 
            BackgroundColour);
        
        LayoutPanelColumn();
        SizeDetailControls();
    }

    private static bool SamePrefix(string left, string right)
    {
        static string Prefix(string key)
        {
            int separator = key.IndexOf(':');
            return separator < 0 ? key : key[..separator];
        }

        return Prefix(left) == Prefix(right);
    }

    protected override void OnResize()
    {
        LayoutPanelColumn();
        SizeDetailControls();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        base.OnUnload();
    }

    private void LayoutPanelColumn()
    {
        panelColumnWidth = Math.Min(42, (int)(Width * 0.3));
        visiblePanelCount = Math.Max(1, Height / PanelHeight);

        int selectedIndex = Math.Max(0, panelControls.IndexOf(activeControl));
        
        scrollOffset = ClampScrollOffset(
            selectedIndex, 
            scrollOffset, 
            visiblePanelCount, 
            panelControls.Count);

        for (int i = 0; i < panelControls.Count; i++) {
            PerformancePanelControl panelControl = panelControls[i];
            bool visible = i >= scrollOffset && i < scrollOffset + visiblePanelCount;

            panelControl.Visible = visible;
            panelControl.Chart.Visible = visible;
            panelControl.X = X;
            panelControl.Width = panelColumnWidth;
            panelControl.Height = PanelHeight;

            if (visible) {
                panelControl.Y = Y + (i - scrollOffset) * PanelHeight;
                panelControl.Resize();
            }
        }
    }

    internal static int ClampScrollOffset(int selectedIndex, int scrollOffset, int visibleCount, int total)
    {
        int maxOffset = Math.Max(0, total - visibleCount);

        if (selectedIndex < scrollOffset) {
            scrollOffset = selectedIndex;
        }
        else if (selectedIndex >= scrollOffset + visibleCount) {
            scrollOffset = selectedIndex - visibleCount + 1;
        }

        return Math.Clamp(scrollOffset, 0, maxOffset);
    }

    private void DrawPanelColumnFiller()
    {
        int filledRows = visiblePanelCount * PanelHeight;

        if (filledRows < Height) {
            DrawRectangle(X, Y + filledRows, panelColumnWidth, Height - filledRows, BackgroundColour);
        }
    }

    private void DrawScrollIndicators()
    {
        if (scrollOffset > 0) {
            DrawGlyph(X + panelColumnWidth, Y, '▲');
        }

        if (scrollOffset + visiblePanelCount < panelControls.Count) {
            DrawGlyph(X + panelColumnWidth, Y + visiblePanelCount * PanelHeight - 1, '▼');
        }
    }

    private void DrawGlyph(int x, int y, char glyph)
    {
        scrollFrame.Clear();
        scrollFrame.MoveTo(x, y);
        scrollFrame.SetColour(appConfig.DefaultTheme.ChartBorder, BackgroundColour);
        scrollFrame.Append(glyph);
        scrollFrame.ResetColour();
        Terminal.Write(scrollFrame.AsSpan());
    }

    private void SetActiveControl(PerformancePanelControl nextControl)
    {
        activeControl.IsSelected = false;
        activeControl = nextControl;

        int previousScrollOffset = scrollOffset;
        LayoutPanelColumn();

        if (scrollOffset != previousScrollOffset) {
            DrawRectangle(
                X, 
                Y, 
                panelColumnWidth, 
                Height, 
                BackgroundColour);
        }

        activeControl.AssociatedControl.Clear();
        activeControl.IsSelected = true;
        activeControl.SetFocus();

        Draw();
    }

    private void SizeDetailControls()
    {
        foreach (Control detail in detailCache.Values) {
            detail.X = X + panelColumnWidth;
            detail.Y = Y;
            detail.Width = Width - panelColumnWidth;
            detail.Height = Height;
            detail.Resize();
        }
    }
}
