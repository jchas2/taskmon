using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Summary2;

// A recursive-split-tree replacement for SummaryControl, built alongside it rather than instead
// of it (see MainScreen2.UseSummaryControl2) so the existing fixed-grid dashboard stays available
// and reverting is a one-line change while this is unproven. Renders appConfig.DefaultSummaryLayout2
// (chosen on Setup's LAYOUTS tab, edited in LayoutDesignerScreen), or the built-in example tree if
// there is none, and rebuilds its panes on Load whenever that default has changed.
public sealed class SummaryControl2 : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly SummaryLayoutRenderer renderer = new();
    private readonly Dictionary<int, Control> paneControls = new();

    private SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

    // The layout the panes were built from - compared against appConfig.DefaultSummaryLayout2 on
    // every Load, so a different default chosen in Setup (or the default re-saved from the
    // designer, which AppConfig replaces with a new instance) shows up when this is next shown.
    private SummaryLayout2? builtFrom;

    private SystemSnapshot? snapshot;
    private int? focusedPaneId;

    public SummaryControl2(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;

        BuildPanes();
    }

    // Only ever called while unloaded (the constructor, or OnLoad before base.OnLoad() loads the
    // new panes) - the previous panes were already unloaded by this control's own Unload().
    private void BuildPanes()
    {
        builtFrom = appConfig.DefaultSummaryLayout2;
        tree = builtFrom?.ToTree() ?? SummaryLayoutTree.CreateExample();

        Controls.Clear();
        paneControls.Clear();
        focusedPaneId = null;

        foreach (SummaryLayoutNode pane in tree.Panes()) {
            Control control = SummaryPaneControlFactory.Create(pane, serviceController, Terminal, appConfig);
            paneControls[pane.Id] = control;
            Controls.Add(control);
        }
    }

    // SummaryControl2 itself draws no border - focus always lives on one of its pane controls,
    // whose own OnGotFocus (where the pane type is a composite) redirects further down again -
    // so a SetFocus() call on this composite needs to be redirected there for the focus-colour
    // cue to reach anything visible.
    protected override void OnGotFocus()
    {
        int paneId = focusedPaneId ?? tree.Panes().First().Id;
        paneControls[paneId].SetFocus();
    }

    protected override void OnDraw()
    {
        OnDrawCharts();
        OnDrawProcesses();

        foreach (Control control in paneControls.Values) {
            control.Draw();
        }
    }

    // Same source data/labels as SummaryControl.OnDrawCharts(), fed to whichever chart panes are
    // actually in the tree - shared with LayoutDesignerScreen via SummaryChartFeeder.
    private void OnDrawCharts()
    {
        if (snapshot == null) {
            return;
        }

        SummaryChartFeeder.Feed(tree, paneControls, snapshot, appConfig);
    }

    private void OnDrawProcesses()
    {
        int processCount = snapshot?.Processes?.Metrics.ProcessCount ?? 0;

        foreach (SummaryLayoutNode pane in tree.Panes()) {
            if (pane.ControlType != PaneControlType.Process || paneControls[pane.Id] is not ProcessControl processControl) {
                continue;
            }

            processControl.HeaderText =
                $"Top {appConfig.SortColumn.ToString().ToUpper()} Processes ({processControl.NumberOfProcesses})    {processCount} Total";
        }
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        Control? focusedPane = FindFocusedPane();

        if (focusedPane == null) {
            return;
        }

        // The focused pane gets first refusal - a pane with its own internal left/right routing
        // (e.g. SystemInfoControl's nav <-> detail), or its own use for up/down (e.g. a Process
        // pane's list scrolling), keeps working unmodified. Only once that pane reports nothing
        // further to do with the key does this control step in - and only for left/right, which
        // cycle through panes in tree order; up/down are never used to switch panes.
        focusedPane.KeyPressed(keyInfo, ref handled);

        if (handled) {
            return;
        }

        int? targetPaneId = keyInfo.Key switch {
            ConsoleKey.LeftArrow => FindAdjacentPane(-1),
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

    private Control? FindFocusedPane() =>
        paneControls.Values.FirstOrDefault(control => control.HasFocus);

    // Cycles through panes in tree order (the same order Panes() yields them in) rather than
    // spatial nearest-neighbour search - simpler, and matches how a linear left/right tab order
    // is expected to behave. Returns null at either end so Left from the first pane and Right
    // from the last pane are left unhandled (Left bubbles out to the outer VIEW MENU).
    private int? FindAdjacentPane(int step)
    {
        List<SummaryLayoutNode> panes = tree.Panes().ToList();
        Control? focusedPane = FindFocusedPane();
        int currentIndex = panes.FindIndex(pane => paneControls[pane.Id] == focusedPane);

        if (currentIndex < 0) {
            return null;
        }

        int targetIndex = currentIndex + step;

        return targetIndex >= 0 && targetIndex < panes.Count ? panes[targetIndex].Id : null;
    }

    protected override void OnLoad()
    {
        if (appConfig.DefaultSummaryLayout2 != builtFrom) {
            BuildPanes();
        }

        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

        foreach (Control control in Controls) {
            control.BackgroundColour = appConfig.Theme.Background;
            control.ForegroundColour = appConfig.Theme.Foreground;
        }

        foreach (SummaryLayoutNode pane in tree.Panes()) {
            if (paneControls[pane.Id] is Chart chart) {
                chart.BackgroundColour = appConfig.Theme.ChartBackground;
                chart.BorderForegroundColour = appConfig.Theme.ChartBorderForeground;
                chart.BorderBackgroundColour = appConfig.Theme.ChartBorderBackground;
                chart.ColourHigh = appConfig.Theme.RangeHighBackground;
                chart.ColourLow = appConfig.Theme.RangeLowBackground;
                chart.ColourMid = appConfig.Theme.RangeMidBackground;
                chart.MetreStyle = appConfig.MetreStyle;
                chart.ShowYAxisScale = appConfig.ShowYAxisScale;
                chart.YAxisColour = appConfig.Theme.ChartYAxis;
            }
        }

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        // Loads every pane (Control.OnLoad's default foreach over Controls). Never Load() them
        // explicitly as well - that double-subscribes panes with their own snapshot handler.
        base.OnLoad();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        snapshot = e.Snapshot;

        try {
            Control.DrawingLockAcquire();
            OnDrawCharts();
            OnDrawProcesses();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    protected override void OnResize() =>
        renderer.Layout(tree, paneControls, X, Y, Width, Height);

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;

        // Unloads every pane (Control.OnUnload's default foreach over Controls).
        base.OnUnload();
    }
}
