using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Summary2;

public sealed class SummaryControl2 : Control
{
    private readonly SummaryLayoutRenderer    renderer     = new();
    private readonly Dictionary<int, Control> paneControls = new();

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;

    private SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();
    
    private SummaryControlLayout? builtFrom;
    private SystemSnapshot?       snapshot;
    
    private int? focusedPaneId;
    private bool isFixedTree;

    public SummaryControl2(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        BuildPanes();
    }

    public SummaryControl2(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig, SummaryLayoutTree fixedTree)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        tree        = fixedTree;
        isFixedTree = true;

        BuildPaneControls();
    }

    private void BuildPaneControls()
    {
        Controls.Clear();
        paneControls.Clear();
        focusedPaneId = null;

        foreach (SummaryLayoutNode pane in tree.Panes()) {
            Control control = SummaryPaneControlFactory.Create(pane, serviceController, Terminal, appConfig);
            paneControls[pane.Id] = control;
            Controls.Add(control);
        }
    }

    private void BuildPanes()
    {
        builtFrom = appConfig.DefaultLayout;
        tree      = builtFrom?.ToTree() ?? SummaryLayoutTree.CreateExample();

        BuildPaneControls();
    }

    private int? FindAdjacentPane(int step)
    {
        List<SummaryLayoutNode> panes       = tree.Panes().ToList();
        Control?                focusedPane = FindFocusedPane();
        
        int currentIndex = panes.FindIndex(pane => paneControls[pane.Id] == focusedPane);

        if (currentIndex < 0) {
            return null;
        }

        int targetIndex = currentIndex + step;

        return targetIndex >= 0 && targetIndex < panes.Count 
            ? panes[targetIndex].Id 
            : null;
    }

    private Control? FindFocusedPane() =>
        paneControls.Values.FirstOrDefault(control => control.HasFocus);

    protected override void OnDraw()
    {
        OnDrawCharts();
        OnDrawProcesses();

        foreach (Control control in paneControls.Values) {
            control.Draw();
        }
    }

    private void OnDrawCharts()
    {
        if (snapshot == null) {
            return;
        }

        SummaryChartFeeder.Feed(
            tree, 
            paneControls, 
            snapshot, 
            appConfig);
    }

    private void OnDrawProcesses()
    {
        int processCount = snapshot?.Processes?.Metrics.ProcessCount ?? 0;

        foreach (SummaryLayoutNode pane in tree.Panes()) {
            if (pane.ControlType != PaneControlType.Process || paneControls[pane.Id] is not ProcessControl processControl) {
                continue;
            }

            processControl.HeaderText = ProcessHeaderText(appConfig.SortColumn, processControl.NumberOfProcesses, processCount);
        }
    }

    protected override void OnGotFocus()
    {
        int paneId = focusedPaneId ?? tree.Panes().First().Id;
        paneControls[paneId].SetFocus();
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
            paneControls[id].SetFocus();
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

        foreach (Control control in paneControls.Values) {
            SummaryPaneTheme.Apply(control, appConfig);
        }

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        // Loads every pane. 
        base.OnLoad();

        if (snapshot != null) {
            foreach (SnapshotMetreControl metre in paneControls.Values.OfType<SnapshotMetreControl>()) {
                metre.Sample(snapshot);
            }
        }
    }

    protected override void OnResize() =>
        renderer.Layout(tree, paneControls, X, Y, Width, Height);

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        snapshot = e.Snapshot;

        lock (DrawingLock) {
            OnDrawCharts();
            OnDrawProcesses();
        }
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;

        // Unloads every pane.
        base.OnUnload();
    }

    internal IReadOnlyDictionary<int, Control> PaneControls => paneControls;

    internal static string ProcessHeaderText(Statistics sortColumn, int numberOfProcesses, int processCount)
    {
        string shown = numberOfProcesses > -1 ? $" ({numberOfProcesses})" : string.Empty;

        return $"Top {sortColumn.ToString().ToUpper()} Processes{shown}    {processCount} Total";
    }
}
