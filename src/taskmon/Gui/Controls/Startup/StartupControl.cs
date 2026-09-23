using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.Gui.Controls.Startup;

// The applications configured to run at logon - the Run / RunOnce registry keys and the Startup
// folders - in one selectable, scrolling table: name, publisher, where it is registered and for
// whom, whether it is enabled, and the command it runs.
public sealed partial class StartupControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly ListView startupView;
    private readonly ListView detailView;

    private StartupInfo? startup;
    private string lastSignature = string.Empty;

    private const int NameColumnWidth = 24;
    private const int PublisherColumnWidth = 24;
    private const int TypeColumnWidth = 16;
    private const int ScopeColumnWidth = 9;
    private const int StatusColumnWidth = 9;

    private const int DetailFieldColumnWidth = 14;
    private const int DetailViewHeight = 9; // border (2) + column headers (1) + 6 field rows

    public StartupControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;

        startupView = new ListView(terminal) {
            EnableScroll = true,
            EnableRowSelect = true,
            ShowColumnHeaders = true,
            ShowBorder = true,
            TabStop = true,
            TabIndex = 2,
            Visible = true,
            EmptyListViewText = "Gathering startup applications…"
        };

        startupView.ColumnHeaders
            .Add(new ListViewColumnHeader("NAME"))
            .Add(new ListViewColumnHeader("PUBLISHER"))
            .Add(new ListViewColumnHeader("TYPE"))
            .Add(new ListViewColumnHeader("SCOPE"))
            .Add(new ListViewColumnHeader("STATUS"))
            .Add(new ListViewColumnHeader("COMMAND"));

        // The main table's columns are too narrow to show a long publisher or command in full;
        // this mirrors the selected row's fields one per line so every value can be read.
        detailView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowColumnHeaders = true,
            ShowBorder = true,
            TabStop = false,
            Visible = true,
            HeaderText = "DETAIL"
        };

        detailView.ColumnHeaders
            .Add(new ListViewColumnHeader("FIELD"))
            .Add(new ListViewColumnHeader("VALUE"));

        Controls.Add(startupView);
        Controls.Add(detailView);
    }

    // Wired to the controller event in OnLoad; tests call it directly.
    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Startup is null) {
            return;
        }

        startup = snapshot.Startup;
        Draw();
    }

    // StartupControl itself draws no border - startupView is the actual bordered, focusable
    // panel - so a SetFocus() call on this composite needs to be redirected down to it for the
    // focus-colour cue to reach anything visible.
    protected override void OnGotFocus() => startupView.SetFocus();

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    protected override void OnDraw()
    {
        if (startup is { } current) {
            EnsureRows(current);
        }

        RefreshDetailPane();

        startupView.Draw();
        detailView.Draw();
    }

    // Rebuilds the row list only when the set of entries changes - installs and uninstalls, or a
    // user enabling or disabling one - rather than every publish, since a rebuild resets the
    // scroll position.
    private void EnsureRows(StartupInfo info)
    {
        string signature = BuildSignature(info.Specs.Entries);

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;
        RebuildRows(info.Specs.Entries);
    }

    private static string BuildSignature(IReadOnlyList<StartupEntry> entries) =>
        entries.Count + "|" + string.Join("|", entries.Select(entry =>
            $"{entry.Name}:{(int)entry.Source}:{(int)entry.Scope}:{(int)entry.State}"));

    // Rebuilds the detail pane from whichever row is currently highlighted in the main table.
    // Cheap enough (six rows) to call on every draw rather than tracking whether the selection or
    // the underlying entry actually changed.
    private void RefreshDetailPane()
    {
        int index = startupView.SelectedIndex;
        IReadOnlyList<StartupEntry> entries = startup?.Specs.Entries ?? [];

        StartupEntry? selected = index >= 0 && index < entries.Count ? entries[index] : null;

        RebuildDetailRows(selected);
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        switch (keyInfo.Key) {
            case ConsoleKey.R:
            case ConsoleKey.F5:
                TryRequestRefresh();
                handled = true;
                return;
        }

        startupView.KeyPressed(keyInfo, ref handled);

        // The main table redraws itself directly (bypassing this control's own OnDraw), so a
        // selection change made by that key press needs an explicit detail-pane refresh here.
        if (handled) {
            RefreshDetailPane();
            detailView.Draw();
        }
    }

    private void TryRequestRefresh()
    {
        try {
            serviceController.GetService<StartupService>().RequestImmediateRefresh();
        }
        catch (InvalidOperationException) {
            // No startup service registered (e.g. under test) - nothing to refresh.
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

        startupView.BackgroundColour = appConfig.Theme.ListViewBackground;
        startupView.ForegroundColour = appConfig.Theme.ListViewForeground;
        startupView.BorderForegroundColour = appConfig.Theme.ListViewBorderForeground;
        startupView.BorderBackgroundColour = appConfig.Theme.ListViewBorderBackground;
        startupView.HeaderBackgroundColour = appConfig.Theme.HeaderBackground;
        startupView.HeaderForegroundColour = appConfig.Theme.HeaderForeground;
        startupView.BackgroundHighlightColour = appConfig.Theme.BackgroundHighlight;
        startupView.ForegroundHighlightColour = appConfig.Theme.ForegroundHighlight;
        startupView.BackgroundHighlightInactiveColour = appConfig.Theme.BackgroundHighlightInactive;
        startupView.ForegroundHighlightInactiveColour = appConfig.Theme.ForegroundHighlightInactive;

        foreach (ListViewColumnHeader columnHeader in startupView.ColumnHeaders) {
            columnHeader.BackgroundColour = appConfig.Theme.HeaderBackground;
            columnHeader.ForegroundColour = appConfig.Theme.HeaderForeground;
        }

        detailView.BackgroundColour = appConfig.Theme.ListViewBackground;
        detailView.ForegroundColour = appConfig.Theme.ListViewForeground;
        detailView.BorderForegroundColour = appConfig.Theme.ListViewBorderForeground;
        detailView.BorderBackgroundColour = appConfig.Theme.ListViewBorderBackground;
        detailView.HeaderBackgroundColour = appConfig.Theme.HeaderBackground;
        detailView.HeaderForegroundColour = appConfig.Theme.HeaderForeground;

        foreach (ListViewColumnHeader columnHeader in detailView.ColumnHeaders) {
            columnHeader.BackgroundColour = appConfig.Theme.HeaderBackground;
            columnHeader.ForegroundColour = appConfig.Theme.HeaderForeground;
        }

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnResize()
    {
        int startupViewHeight = Math.Max(1, Height - DetailViewHeight);

        startupView.X = X;
        startupView.Y = Y;
        startupView.Width = Width;
        startupView.Height = startupViewHeight;

        int fixedWidth = NameColumnWidth + PublisherColumnWidth + TypeColumnWidth + ScopeColumnWidth + StatusColumnWidth;

        startupView.ColumnHeaders[0].Width = NameColumnWidth;
        startupView.ColumnHeaders[1].Width = PublisherColumnWidth;
        startupView.ColumnHeaders[2].Width = TypeColumnWidth;
        startupView.ColumnHeaders[3].Width = ScopeColumnWidth;
        startupView.ColumnHeaders[4].Width = StatusColumnWidth;
        startupView.ColumnHeaders[5].Width = Math.Max(1, Width - fixedWidth - 3);

        detailView.X = X;
        detailView.Y = Y + startupViewHeight;
        detailView.Width = Width;
        detailView.Height = DetailViewHeight;

        detailView.ColumnHeaders[0].Width = DetailFieldColumnWidth;
        detailView.ColumnHeaders[1].Width = Math.Max(1, Width - DetailFieldColumnWidth - 3);

        base.OnResize();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        startupView.Items.Clear();
        detailView.Items.Clear();
        lastSignature = string.Empty;

        base.OnUnload();
    }
}
