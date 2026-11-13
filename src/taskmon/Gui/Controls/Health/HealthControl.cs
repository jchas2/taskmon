using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Health;

namespace Task.Monitor.Gui.Controls.Health;

public sealed partial class HealthControl : Control
{
    private const string NoCausesText  = "Nothing under pressure.";
    private const string NoEventsText  = "No events recorded yet.";
    private const string NoProcessText = "No process stands out.";
    private const string WaitingText   = "Gathering metrics…";

    private const int MinPaneHeight = 4;

    private const int BorderWidth                = 3;
    private const int EventTimeColumnWidth       = 13;
    private const int EventTitleColumnWidth      = 20;
    private const int ExtraColumnsMinWidth       = 84;
    private const int MaxCauseProcessColumnWidth = 28;
    private const int MaxCauseBehindColumnWidth  = 22;
    private const int MaxEventProcessesWidth     = 26;
    private const int MaxUtilisationColumnWidth  = 28;
    private const int MinCauseBehindColumnWidth  = 12;
    private const int MinCauseProcessColumnWidth = 16;
    private const int ResourceErrorsColumnWidth  = 20;
    private const int ResourceNameColumnWidth    = 10;
    private const int ResourceStatusColumnWidth  = 12;


    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly ListView          resourcesView;
    private readonly ListView          causesView;
    private readonly ListView          eventsView;
    private readonly ListView[]        panes;

    private HealthInfo? health;

    private string lastCausesSignature = string.Empty;
    private string lastEventsSignature = string.Empty;

    private IReadOnlyList<HealthCulprit> visibleCauses = [];

    public event EventHandler<int>? ProcessRequested;

    public HealthControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        resourcesView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = false,
            Visible           = true,
            EmptyListViewText = WaitingText
        };

        resourcesView.ColumnHeaders
            .Add(new ListViewColumnHeader("RESOURCE"))
            .Add(new ListViewColumnHeader("STATUS"))
            .Add(new ListViewColumnHeader("UTILISATION"))
            .Add(new ListViewColumnHeader("SATURATION"))
            .Add(new ListViewColumnHeader("ERRORS"));

        causesView = new ListView(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = true,
            TabIndex          = 2,
            Visible           = true,
            HeaderText        = "LIKELY CAUSES",
            EmptyListViewText = NoCausesText
        };

        causesView.ColumnHeaders
            .Add(new ListViewColumnHeader("PROCESS"))
            .Add(new ListViewColumnHeader("USING"))
            .Add(new ListViewColumnHeader("BEHIND"));

        eventsView = new ListView(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = true,
            TabIndex          = 3,
            Visible           = true,
            HeaderText        = "RECENT EVENTS",
            EmptyListViewText = NoEventsText
        };

        eventsView.ColumnHeaders
            .Add(new ListViewColumnHeader("TIME"))
            .Add(new ListViewColumnHeader("EVENT"))
            .Add(new ListViewColumnHeader("DETAIL"))
            .Add(new ListViewColumnHeader("PROCESSES"));

        panes = [causesView, eventsView];

        Controls.Add(resourcesView);
        Controls.Add(causesView);
        Controls.Add(eventsView);
    }

    internal ListView CausesView => causesView;

    private void CausesViewOnItemSelected(object? sender, ListViewItemEventArgs e)
    {
        int index = causesView.SelectedIndex;

        if (index >= 0 && index < visibleCauses.Count) {
            ProcessRequested?.Invoke(this, visibleCauses[index].Pid);
        }
    }

    internal ListView EventsView => eventsView;

    private int FocusedPaneIndex => Array.FindIndex(panes, pane => pane.Focused);

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    protected override void OnDraw()
    {
        if (health is { } current) {
            RefreshRows(current);
        }

        resourcesView.Draw();

        foreach (ListView pane in panes) {
            pane.Draw();
        }
    }

    protected override void OnGotFocus()
    {
        int index = Math.Max(0, FocusedPaneIndex);
        panes[index].SetFocus();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        int focused = FocusedPaneIndex;

        if (keyInfo.Key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow) {
            int target = keyInfo.Key == ConsoleKey.LeftArrow ? focused - 1 : focused + 1;

            // Left from the first pane stays unhandled, so the main screen returns to the menu.
            if (target >= 0 && target < panes.Length && panes[target].Visible) {
                panes[target].SetFocus();
                handled = true;
                Draw();
            }

            return;
        }

        if (focused >= 0) {
            panes[focused].KeyPressed(keyInfo, ref handled);
        }
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(resourcesView, appConfig.Theme);
        CommonControl.Theme(causesView,    appConfig.Theme);
        CommonControl.Theme(eventsView,    appConfig.Theme);

        causesView.ItemSelected                 += CausesViewOnItemSelected;
        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    protected override void OnResize()
    {
        int resourcesHeight = Math.Min(Height, ResourcesViewHeight(Enum.GetValues<HealthResource>().Length));
        int remaining       = Math.Max(0, Height - resourcesHeight);
        int causesHeight    = Math.Min(remaining, Math.Max(MinPaneHeight, remaining / 2));
        int eventsHeight    = remaining - causesHeight;

        resourcesView.X      = X;
        resourcesView.Y      = Y;
        resourcesView.Width  = Width;
        resourcesView.Height = resourcesHeight;

        causesView.X       = X;
        causesView.Y       = Y + resourcesHeight;
        causesView.Width   = Width;
        causesView.Height  = causesHeight;
        causesView.Visible = causesHeight >= MinPaneHeight;

        eventsView.X       = X;
        eventsView.Y       = Y + resourcesHeight + causesHeight;
        eventsView.Width   = Width;
        eventsView.Height  = eventsHeight;
        eventsView.Visible = eventsHeight >= MinPaneHeight;

        SizeColumns();

        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        causesView.ItemSelected                 -= CausesViewOnItemSelected;

        resourcesView.Items.Clear();
        causesView.Items.Clear();
        eventsView.Items.Clear();

        lastCausesSignature = string.Empty;
        lastEventsSignature = string.Empty;
        visibleCauses       = [];

        base.OnUnload();
    }

    private static int ResourcesViewHeight(int rows) => rows + 3;  // border (2) + column headers (1)

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Health is null) {
            return;
        }

        health = snapshot.Health;
        Draw();
    }

    private void SizeColumns()
    {
        bool showExtras  = Width >= ExtraColumnsMinWidth;
        int  errorsWidth = showExtras ? ResourceErrorsColumnWidth : 0;
        int  sharedWidth = Math.Max(2, Width - BorderWidth - ResourceNameColumnWidth - ResourceStatusColumnWidth - errorsWidth);
        int  utilisation = Math.Min(MaxUtilisationColumnWidth, sharedWidth / 2);

        resourcesView.ColumnHeaders[0].Width = ResourceNameColumnWidth;
        resourcesView.ColumnHeaders[1].Width = ResourceStatusColumnWidth;
        resourcesView.ColumnHeaders[2].Width = utilisation;
        resourcesView.ColumnHeaders[3].Width = sharedWidth - utilisation;
        resourcesView.ColumnHeaders[4].Width = errorsWidth;

        int processWidth = Math.Clamp(Width / 4, MinCauseProcessColumnWidth, MaxCauseProcessColumnWidth);
        int behindWidth  = Math.Clamp(Width / 5, MinCauseBehindColumnWidth, MaxCauseBehindColumnWidth);

        causesView.ColumnHeaders[0].Width = processWidth;
        causesView.ColumnHeaders[1].Width = Math.Max(1, Width - processWidth - behindWidth - BorderWidth);
        causesView.ColumnHeaders[2].Width = behindWidth;

        int processesWidth = showExtras ? Math.Min(MaxEventProcessesWidth, Width / 4) : 0;

        eventsView.ColumnHeaders[0].Width = EventTimeColumnWidth;
        eventsView.ColumnHeaders[1].Width = EventTitleColumnWidth;
        eventsView.ColumnHeaders[2].Width = Math.Max(1, Width - EventTimeColumnWidth - EventTitleColumnWidth - processesWidth - BorderWidth);
        eventsView.ColumnHeaders[3].Width = processesWidth;
    }
}

