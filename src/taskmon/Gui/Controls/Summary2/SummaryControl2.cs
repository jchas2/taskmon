using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Gui.Controls.Cpu;
using Task.Monitor.Gui.Controls.DiskSpace;
using Task.Monitor.Gui.Controls.Health;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Network;

namespace Task.Monitor.Gui.Controls.Summary2;

public sealed class SummaryControl2 : Control
{
    private const double MidLoadThreshold  = 0.5;
    private const double HighLoadThreshold = 0.85;
    private const int    NetworkPeakWindow = 60;
    private const double NetworkPeakFloor  = 64 * 1024;
    private const int    MetrePaneHeight   = 3;

    internal const Statistics DefaultProcessColumns = Statistics.Process | Statistics.Pid | Statistics.Cpu | Statistics.Mem;
    internal const Statistics DefaultProcessSort    = Statistics.Cpu;

#if __APPLE__
    internal static readonly PaneControlType[] UnavailableControlTypes = [];
#else
    internal static readonly PaneControlType[] UnavailableControlTypes = [PaneControlType.Fans, PaneControlType.NpuMetre, PaneControlType.Npu];
#endif

    private readonly Dictionary<int, Rectangle> paneBounds = new();

    private readonly Dictionary<int, (SummaryPane Pane, Control Control, Action<SystemSnapshot>? Feed)> panes = new();

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly bool              showOutlines;

    private LayoutTree<SummaryPane> tree;
    private SummaryControlLayout?   builtFrom;
    private SystemSnapshot?         snapshot;

    private int? focusedPaneId;
    private bool isFixedTree;
    private bool isLoaded;

    public SummaryControl2(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        BuildPanes();
    }

    public SummaryControl2(
        ServiceController       serviceController,
        ISystemTerminal         terminal,
        AppConfig               appConfig,
        LayoutTree<SummaryPane> fixedTree,
        bool                    showOutlines = false) : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;
        this.showOutlines      = showOutlines;

        tree        = fixedTree;
        isFixedTree = true;

        SyncPanes();
    }

    [MemberNotNull(nameof(tree))]
    private void BuildPanes()
    {
        builtFrom = appConfig.DefaultLayout;
        tree      = builtFrom?.ToTree() ?? SummaryControlLayout.Fallback();

        Controls.Clear();
        panes.Clear();
        SyncPanes();
    }

    private (Control, Action<SystemSnapshot>) ChartPane(
        string               title,
        bool                 autoScale,
        Func<double, string> scale,
        Func<bool>           showLabel,
        Func<SystemSnapshot, (double Value, string Label)?> read)
    {
        Chart chart = new(Terminal) {
            Text                      = title,
            AutoScale                 = autoScale,
            CustomYAxisScaleFormatter = scale
        };

        return (chart, snapshot => {
            if (read(snapshot) is { } reading) {
                chart.LabelSeries = showLabel() ? reading.Label : string.Empty;
                chart.Add(reading.Value);
            }
        });
    }

    private (Control Control, Action<SystemSnapshot>? Feed) CreatePane(SummaryPane pane) =>
        pane.ControlType switch {
            PaneControlType.Cpu => ChartPane(
                Strings.ChartTitleCpu, 
                false, 
                Chart.FormatYScalePercentage, 
                () => appConfig.ShowMetreCpuNumerically,
                s => s.Cpu?.Metrics is { } cpu
                    ? (cpu.CpuPercentKernelTime + cpu.CpuPercentUserTime, 
                        string.Format(CultureInfo.CurrentCulture, Strings.SummaryCpuChartLabel, cpu.CpuPercentKernelTime + cpu.CpuPercentUserTime, cpu.CpuPercentKernelTime, cpu.CpuPercentUserTime))
                    : null),
            PaneControlType.Memory => ChartPane(
                Strings.ChartTitleMemory, 
                false, 
                Chart.FormatYScalePercentage, 
                () => appConfig.ShowMetreMemoryNumerically,
                s => s.Memory?.Metrics is { } memory 
                    ? (memory.ToMemoryRatio(), memory.ToMemoryRatioFormattedBytes()) 
                    : null),
#if __APPLE__
            PaneControlType.VirtualMemory => ChartPane(
                Strings.ChartTitleSwap, 
                false, 
                Chart.FormatYScalePercentage, 
                () => appConfig.ShowMetreSwapNumerically,
#else
            PaneControlType.VirtualMemory => ChartPane(
                Strings.ChartTitleVirtualMemory, 
                false, 
                Chart.FormatYScalePercentage, 
                () => appConfig.ShowMetreSwapNumerically,
#endif
                s => s.Memory?.Metrics is { } memory 
                    ? (memory.ToPageFileMemoryRatio(), memory.ToPageFileMemoryRatioFormattedBytes()) 
                    : null),
            PaneControlType.Disk => ChartPane(
                Strings.ChartTitleDisk, 
                true, 
                Chart.FormatYScaleByteRate, 
                () => appConfig.ShowMetreDiskNumerically,
                s => s.Disk?.Metrics is { } disk 
                    ? (disk.ToDiskTransferBytesPerSecond(), disk.ToDiskTransferRate()) 
                    : null),
            PaneControlType.Gpu => ChartPane(
                Strings.ChartTitleGpu, 
                false, 
                Chart.FormatYScalePercentage, 
                () => appConfig.ShowMetreGpuNumerically,
                s => s.Gpu?.Metrics is { } gpu 
                    ? (gpu.GpuPercentTime, gpu.ToGpuPercentage()) 
                    : null),
            PaneControlType.GpuMemory => ChartPane(
                Strings.ChartTitleGpuMemory, 
                false, 
                Chart.FormatYScalePercentage, 
                () => appConfig.ShowMetreGpuMemNumerically,
                s => s.Gpu?.Metrics is { } gpu 
                    ? (gpu.ToGpuMemoryRatio(), gpu.ToGpuMemoryRatioFormattedBytes()) 
                    : null),
            PaneControlType.NetworkReceived => ChartPane(
                Strings.ChartTitleNetworkReceived, 
                true, 
                Chart.FormatYScaleByteRate, 
                () => appConfig.ShowMetreNetworkNumerically,
                s => s.Network?.Metrics is { } network 
                    ? (network.ReceiveBytesPerSecond, network.ToNetworkReceiveRate()) 
                    : null),
            PaneControlType.NetworkSent => ChartPane(
                Strings.ChartTitleNetworkSent, 
                true, 
                Chart.FormatYScaleByteRate, 
                () => appConfig.ShowMetreNetworkNumerically,
                s => s.Network?.Metrics is { } network 
                    ? (network.SendBytesPerSecond, network.ToNetworkSendRate()) 
                    : null),
#if __APPLE__
            PaneControlType.Npu => ChartPane(
                Strings.ChartTitleNpu, 
                false, 
                Chart.FormatYScalePercentage, 
                () => appConfig.ShowMetreNpuNumerically,
                s => s.Npu?.Metrics.Devices.FirstOrDefault() is { } npu
                    ? (npu.ActiveRatio, s.Power?.Metrics.NeuralEnginePower() is { } watts ? $"{npu.ToNpuPercentage()} · {watts.ToWattText()}" : npu.ToNpuPercentage())
                    : null),
#endif

            PaneControlType.CpuMetre => MetrePane(
                Strings.MetreTitleCpu,
                s => s.Cpu?.Metrics is { } cpu
                    ? ([cpu.CpuPercentKernelTime, cpu.CpuPercentUserTime], FormatPercentage(cpu.CpuPercentKernelTime + cpu.CpuPercentUserTime))
                    : null,
                theme => [theme.PerformanceCpuKernel, theme.PerformanceCpuUser]),
            PaneControlType.GpuMetre => MetrePane(
                Strings.MetreTitleGpu,
                s => s.Gpu?.Metrics is { } gpu 
                    ? ([gpu.GpuPercentTime], FormatPercentage(gpu.GpuPercentTime)) 
                    : null),
            PaneControlType.GpuMemoryMetre => MetrePane(
                Strings.MetreTitleGpuMemory,
                s => s.Gpu?.Metrics is { } gpu 
                    ? ([gpu.ToGpuMemoryRatio()], gpu.ToGpuMemoryRatioFormattedBytes()) 
                    : null),
            PaneControlType.MemoryMetre => MetrePane(
                Strings.MetreTitleMemory,
                s => s.Memory?.Metrics is { } memory 
                    ? ([memory.ToMemoryRatio()], memory.ToMemoryRatioFormattedBytes()) 
                    : null),
#if __APPLE__
            PaneControlType.VirtualMemoryMetre => MetrePane(
                Strings.MetreTitleSwap,
#else
            PaneControlType.VirtualMemoryMetre => MetrePane(
                Strings.MetreTitleVirtual,
#endif
                s => s.Memory?.Metrics is { } memory 
                    ? ([memory.ToPageFileMemoryRatio()], memory.ToPageFileMemoryRatioFormattedBytes()) 
                    : null),
            PaneControlType.DiskMetre => MetrePane(
                Strings.MetreTitleDisk,
                s => s.Disk?.Metrics is { } disk
                    ? ([disk.ToDiskActiveTimeRatio()], $"{FormatPercentage(disk.ToDiskActiveTimeRatio())} · {disk.ToDiskTransferRate()}")
                    : null),
            PaneControlType.NetworkSendMetre => NetworkMetrePane(
                Strings.MetreTitleNetworkSend,
                network => (network.SendBytesPerSecond, network.ToNetworkSendRate())),
            PaneControlType.NetworkReceiveMetre => NetworkMetrePane(
                Strings.MetreTitleNetworkReceive,
                network => (network.ReceiveBytesPerSecond, network.ToNetworkReceiveRate())),
#if __APPLE__
            PaneControlType.NpuMetre => MetrePane(Strings.MetreTitleNpu,
                s => s.Npu?.Metrics.Devices.FirstOrDefault() is { } npu
                    ? ([npu.ActiveRatio], s.Power?.Metrics.NeuralEnginePower() is { } watts
                        ? $"{FormatPercentage(npu.ActiveRatio)} · {watts.ToWattText()}"
                        : FormatPercentage(npu.ActiveRatio))
                    : null),
#endif

            // A sort changed here lasts only as long as the pane, so SortChanged isn't kept.
            PaneControlType.Process => (new ProcessControl(serviceController, Terminal, appConfig) {
                TabStop                = true,
                VisibleColumnsOverride = ShownProcessColumns(pane),
                SortColumn             = pane.SortColumn ?? DefaultProcessSort,
                SortAscending          = pane.SortAscending
            }, null),
            PaneControlType.DiskSpace     => (new DiskSpaceControl(serviceController, Terminal, appConfig)     { TabStop = true }, null),
            PaneControlType.CpuCores      => (new CpuCoresControl(serviceController, Terminal, appConfig)      { TabStop = false }, null),
            PaneControlType.Power         => (new PowerControl(serviceController, Terminal, appConfig)         { TabStop = false }, null),
            PaneControlType.Health        => (new HealthControl(serviceController, Terminal, appConfig)        { TabStop = false, ShowCausesAndEvents = false }, null),
#if __APPLE__
            PaneControlType.Fans          => (new FansControl(serviceController, Terminal, appConfig)          { TabStop = false }, null),
#endif
                                        _ => (new EmptyPaneControl(Terminal), null)
        };

    private int? FindAdjacentPane(int step)
    {
        List<int> paneIds     = tree.Panes().Select(pane => pane.Id).ToList();
        Control?  focusedPane = FindFocusedPane();

        int currentIndex = paneIds.FindIndex(paneId => panes[paneId].Control == focusedPane);

        if (currentIndex < 0) {
            return null;
        }

        int targetIndex = currentIndex + step;

        return targetIndex >= 0 && targetIndex < paneIds.Count
            ? paneIds[targetIndex]
            : null;
    }

    private Control? FindFocusedPane() =>
        panes.Values.Select(pane => pane.Control).FirstOrDefault(control => control.HasFocus);

    internal (int SplitId, bool InFirst)? FindResizableSplit(int paneId)
    {
        bool paneIsFixed = FixedHeight(paneId) is not null;
        int  childId     = paneId;

        while (tree.FindParentSplitId(childId) is { } parentId) {
            LayoutNode<SummaryPane> parent = tree.Nodes[parentId];

            bool ratioApplies = parent.Orientation == Orientation.Row ||
                (!paneIsFixed && FixedHeight(parent.FirstId) is null && FixedHeight(parent.SecondId) is null);

            if (ratioApplies) {
                return (parentId, parent.FirstId == childId);
            }

            childId = parentId;
        }

        return null;
    }

    internal int? FixedHeight(int nodeId)
    {
        LayoutNode<SummaryPane> node = tree.Nodes[nodeId];

        if (!node.IsSplit) {
            return panes.TryGetValue(nodeId, out var pane)
                ? RequiredHeight(pane.Control)
                : null;
        }

        if (FixedHeight(node.FirstId) is not { } first || FixedHeight(node.SecondId) is not { } second) {
            return null;
        }

        return node.Orientation == Orientation.Column
            ? first + second
            : Math.Max(first, second);
    }

    private static string FormatPercentage(double ratio) => MetreControl.FormatLegendPercentage(ratio);

    private (Control, Action<SystemSnapshot>) MetrePane(
        string                                                title,
        Func<SystemSnapshot, (double[] Values, string Text)?> read,
        Func<Theme, Color[]>?                                 colours = null)
    {
        MetreControl metre = new(Terminal) {
            Border       = true,
            MetreStyle   = MetreControlStyle.Dots,
            ShowLegend   = false,
            Rows         = 1,
            TabStop      = false,
            Text         = title,
            TextPosition = MetreTextPosition.TopLeft
        };

        foreach (Color colour in colours?.Invoke(appConfig.Theme) ?? [appConfig.Theme.RangeLowBackground]) {
            metre.AddSeries(title, colour);
        }

        return (metre, snapshot => {
            if (read(snapshot) is not { } reading) {
                return;
            }

            Theme   theme  = appConfig.Theme;
            Color[] shades = colours?.Invoke(theme) ?? [reading.Values[0] switch {
                >= HighLoadThreshold => theme.RangeHighBackground,
                >= MidLoadThreshold  => theme.RangeMidBackground,
                                   _ => theme.RangeLowBackground
            }];

            for (int i = 0; i < shades.Length; i++) {
                metre.Series[i].Colour = shades[i];
            }

            metre.Text = $"{title} {reading.Text}";
            metre.SetValues(reading.Values);
        });
    }

    private (Control, Action<SystemSnapshot>) NetworkMetrePane(string title, Func<NetworkMetrics, (double BytesPerSecond, string Text)> read)
    {
        RollingPeak peak = new(NetworkPeakWindow, NetworkPeakFloor);

        return MetrePane(title,
            s => s.Network?.Metrics is { } network && read(network) is var (rate, text) 
                ? ([peak.Add(rate)], text) 
                : null,
            theme => [theme.RangeLowBackground]);
    }

    protected override void OnDraw()
    {
        // Note: Charts are updated when a new snapshot arrives.
        OnDrawProcesses();

        foreach ((int id, (_, Control control, _)) in panes) {
            control.Draw();
            int shownHeight = control.Visible ? control.Height : 0;

            if (RequiredHeight(control) is not null &&
                paneBounds.TryGetValue(id, out Rectangle bounds) && bounds.Height > shownHeight) {

                DrawRectangle(
                    bounds.X,
                    bounds.Y + shownHeight,
                    bounds.Width,
                    bounds.Height - shownHeight,
                    BackgroundColour);
            }
        }
    }

    private void OnDrawProcesses()
    {
        int processCount = snapshot?.Processes?.Metrics.ProcessCount ?? 0;

        foreach (ProcessControl processControl in panes.Values.Select(pane => pane.Control).OfType<ProcessControl>()) {
            processControl.HeaderText = ProcessHeaderText(processControl.SortColumn, processCount);
        }
    }

    protected override void OnGotFocus()
    {
        int paneId = focusedPaneId ?? tree.Panes().First().Id;
        panes[paneId].Control.SetFocus();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        Control? focusedPane = FindFocusedPane();

        if (focusedPane == null) {
            return;
        }

        focusedPane.KeyPressed(keyInfo, ref handled);

        if (handled) {
            return;
        }

        int? targetPaneId = keyInfo.Key switch {
            ConsoleKey.LeftArrow  => FindAdjacentPane(-1),
            ConsoleKey.RightArrow => FindAdjacentPane(1),
            _ => null
        };

        if (targetPaneId is { } id) {
            focusedPaneId = id;
            panes[id].Control.SetFocus();
            handled = true;
            Draw();
        }
    }

    protected override void OnLoad()
    {
        if (!isFixedTree && appConfig.DefaultLayout != builtFrom) {
            BuildPanes();
        }

        CommonControl.ThemeDefaults(this, appConfig.Theme);

        foreach ((_, Control control, _) in panes.Values) {
            ThemePane(control);
        }

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        // Loads every pane.
        base.OnLoad();
        isLoaded = true;

        if (snapshot != null) {
            foreach ((_, Control control, Action<SystemSnapshot>? feed) in panes.Values) {
                if (control is MetreControl) {
                    feed?.Invoke(snapshot);
                }
            }
        }
    }

    protected override void OnResize()
    {
        paneBounds.Clear();

        void Arrange(int nodeId, int x, int y, int width, int height)
        {
            LayoutNode<SummaryPane> node = tree.Nodes[nodeId];

            if (!node.IsSplit) {
                paneBounds[nodeId] = new Rectangle(x, y, width, height);

                if (panes.TryGetValue(nodeId, out var pane)) {
                    int? fixedHeight = RequiredHeight(pane.Control);

                    pane.Control.X       = x;
                    pane.Control.Y       = y;
                    pane.Control.Width   = width;
                    pane.Control.Height  = Math.Min(height, fixedHeight ?? height);
                    pane.Control.Visible = height >= (fixedHeight ?? 0);
                    pane.Control.Resize();
                }

                return;
            }

            if (node.Orientation == Orientation.Row) {
                int firstWidth = (int)(width * node.Ratio);

                Arrange(
                    node.FirstId, 
                    x,              
                    y, 
                    firstWidth,         
                    height);
                
                Arrange(
                    node.SecondId, 
                    x + firstWidth, 
                    y, 
                    width - firstWidth, 
                    height);
                
                return;
            }

            int firstHeight = (FixedHeight(node.FirstId), FixedHeight(node.SecondId)) switch {
                ({ } fixedFirst, _)     => Math.Min(fixedFirst, height),
                (null, { } fixedSecond) => height - Math.Min(fixedSecond, height),
                                      _ => (int)(height * node.Ratio)
            };

            Arrange(
                node.FirstId,  
                x, 
                y,               
                width, 
                firstHeight);
            
            Arrange(
                node.SecondId, 
                x, 
                y + firstHeight, 
                width, 
                height - firstHeight);
        }

        Arrange(
            tree.RootId, 
            X, 
            Y, 
            Width, 
            Height);
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        snapshot = e.Snapshot;

        lock (DrawingLock) {
            foreach ((_, _, Action<SystemSnapshot>? feed) in panes.Values) {
                feed?.Invoke(e.Snapshot);
            }

            OnDrawProcesses();
        }
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        isLoaded = false;

        // Unloads every pane.
        base.OnUnload();
    }

    private EmptyPaneControl OutlinePane(SummaryPane pane)
    {
        PaneControlType type = pane.ControlType;

        if (UnavailableControlTypes.Contains(type)) {
            return new EmptyPaneControl(Terminal) {
                Label = string.Format(CultureInfo.CurrentCulture, Strings.SummaryPaneMacOSOnly, type.ToDisplayText())
            };
        }

        bool isMetre = type is PaneControlType.CpuMetre or PaneControlType.GpuMetre or PaneControlType.GpuMemoryMetre
            or PaneControlType.MemoryMetre or PaneControlType.VirtualMemoryMetre or PaneControlType.DiskMetre
            or PaneControlType.NetworkSendMetre or PaneControlType.NetworkReceiveMetre or PaneControlType.NpuMetre;

        Statistics shown = ShownProcessColumns(pane);
        Statistics sort  = pane.SortColumn ?? DefaultProcessSort;
        string     arrow = pane.SortAscending ? "↑" : "↓";

        string columns = type == PaneControlType.Process
            ? string.Join(", ", Enum.GetValues<Statistics>()
                .Where(column => column is not (Statistics.Process or Statistics.Pid) && shown.HasFlag(column))
                .Select(column => column == sort ? $"{column.ToDisplayText()} {arrow}" : column.ToDisplayText()))
            : string.Empty;

        return new EmptyPaneControl(Terminal) {
            Label = type == PaneControlType.Empty ? Strings.SummaryEmptyPane : type.ToDisplayText(),

            Detail = columns.Length > 0 ? columns : null,

            Hint = type switch {
                PaneControlType.Empty => PaneHint.None,
                PaneControlType.CpuCores => PaneHint.Metre,
                PaneControlType.Cpu or PaneControlType.Memory or PaneControlType.VirtualMemory or PaneControlType.Disk
                    or PaneControlType.Gpu or PaneControlType.GpuMemory or PaneControlType.NetworkSent
                    or PaneControlType.NetworkReceived or PaneControlType.Npu => PaneHint.Chart,
                _ => isMetre ? PaneHint.Metre : PaneHint.List
            },

            FixedHeight = isMetre                         ? MetrePaneHeight
                        : type == PaneControlType.Health ? HealthControl.ResourcesViewHeight
                                                          : null
        };
    }

    internal IReadOnlyDictionary<int, Rectangle> PaneBounds => paneBounds;

    internal IReadOnlyDictionary<int, Control> PaneControls =>
        panes.ToDictionary(pane => pane.Key, pane => pane.Value.Control);

    internal static string ProcessHeaderText(Statistics sortColumn, int processCount) =>
        string.Format(
            CultureInfo.CurrentCulture, 
            Strings.SummaryTopProcesses, 
            sortColumn.ToString().ToUpperInvariant(), 
            processCount);

    // These controls don't scroll or stretch, so they keep their content's height
    // and the rest of a column split goes to the other pane.
    private static int? RequiredHeight(Control control) => control switch {
        MetreControl metre                           => metre.RequiredHeight,
        HealthControl { ShowCausesAndEvents: false } => HealthControl.ResourcesViewHeight,
        EmptyPaneControl { FixedHeight: { } height } => height,
                                                   _ => null
    };

    internal void Show(LayoutTree<SummaryPane> newTree)
    {
        tree        = newTree;
        isFixedTree = true;

        SyncPanes();
    }

    internal static Statistics ShownProcessColumns(SummaryPane pane) =>
        (pane.ProcessColumns ?? DefaultProcessColumns) | (pane.SortColumn ?? DefaultProcessSort);

    private void SyncPanes()
    {
        Dictionary<int, SummaryPane> wanted = tree.Panes().ToDictionary(pane => pane.Id, pane => pane.Pane);

        foreach ((int id, (SummaryPane pane, Control control, _)) in panes.ToList()) {
            if (wanted.GetValueOrDefault(id) == pane) {
                continue;
            }

            if (isLoaded) {
                control.Unload();
            }

            Controls.Remove(control);
            panes.Remove(id);
        }

        foreach ((int id, SummaryPane pane) in wanted) {
            if (panes.ContainsKey(id)) {
                continue;
            }

            (Control control, Action<SystemSnapshot>? feed) = showOutlines
                ? (OutlinePane(pane), null)
                : CreatePane(pane);

            panes[id] = (pane, control, feed);
            Controls.Add(control);

            if (isLoaded) {
                ThemePane(control);
                control.Load();
            }
        }
    }

    private void ThemePane(Control control)
    {
        Theme theme = appConfig.Theme;

        CommonControl.ThemeDefaults(control, theme);

        switch (control) {
            case Chart chart:
                CommonControl.Theme(chart, theme);

                chart.MetreStyle     = appConfig.MetreStyle;
                chart.ShowGrid       = appConfig.ShowLargeMetreGrid;
                chart.ShowYAxisScale = appConfig.ShowYAxisScale;
                break;

            case MetreControl metre:
                CommonControl.Theme(metre, theme);
                break;

            case EmptyPaneControl outline:
                (outline.BackgroundColour, outline.ForegroundColour, outline.BorderColour, outline.HintColour) = outline.Hint switch {
                    PaneHint.Chart => (theme.ChartBackground,    theme.ChartTitle,         theme.ChartBorderForeground,    theme.RangeLowBackground),
                    PaneHint.Metre => (theme.MetreBackground,    theme.MetreForeground,    theme.MetreBorderForeground,    theme.RangeLowBackground),
                    PaneHint.List  => (theme.ListViewBackground, theme.ListViewForeground, theme.ListViewBorderForeground, theme.ListViewBorderForeground),
                                 _ => (outline.BackgroundColour, outline.ForegroundColour, theme.ChartBorderForeground,    theme.ChartBorderForeground)
                };
                break;

            case CpuCoresControl:
                control.BorderColour = theme.ChartBorderForeground;
                break;
        }
    }
}
