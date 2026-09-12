using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.InstalledApps;

namespace Task.Monitor.Gui.Controls.InstalledApps;

// The applications registered under the Windows Uninstall registry keys - the same source
// Programs and Features / Settings > Apps reads - in one selectable, scrolling table.
public sealed partial class InstalledAppsControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly ListView installedAppsView;
    private readonly ListView detailView;

    private InstalledAppsInfo? installedApps;
    private string lastSignature = string.Empty;

    private const int NameColumnWidth = 30;
    private const int VersionColumnWidth = 14;
    private const int PublisherColumnWidth = 24;
    private const int ScopeColumnWidth = 9;
    private const int InstallDateColumnWidth = 12;
    private const int SizeColumnWidth = 10;

    private const int DetailFieldColumnWidth = 14;
    private const int DetailViewHeight = 10; // border (2) + column headers (1) + 7 field rows
    private const int DetailGutter = 1;

    public InstalledAppsControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;

        installedAppsView = new ListView(terminal) {
            EnableScroll = true,
            EnableRowSelect = true,
            ShowColumnHeaders = true,
            ShowBorder = true,
            TabStop = true,
            TabIndex = 2,
            Visible = true,
            EmptyListViewText = "Gathering installed applications…"
        };

        installedAppsView.ColumnHeaders
            .Add(new ListViewColumnHeader("NAME"))
            .Add(new ListViewColumnHeader("VERSION"))
            .Add(new ListViewColumnHeader("PUBLISHER"))
            .Add(new ListViewColumnHeader("SCOPE"))
            .Add(new ListViewColumnHeader("INSTALLED"))
            .Add(new ListViewColumnHeader("SIZE"))
            .Add(new ListViewColumnHeader("LOCATION"));

        // The main table's columns are too narrow to show a long publisher or install location in
        // full; this mirrors the selected row's fields one per line so every value can be read.
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

        Controls.Add(installedAppsView);
        Controls.Add(detailView);
    }

    // Wired to the controller event in OnLoad; tests call it directly.
    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.InstalledApps is null) {
            return;
        }

        installedApps = snapshot.InstalledApps;
        Draw();
    }

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();

            if (installedApps is { } current) {
                EnsureRows(current);
            }

            RefreshDetailPane();

            installedAppsView.Draw();
            detailView.Draw();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    // Rebuilds the row list only when the set of apps changes - installs and uninstalls - rather
    // than every publish, since a rebuild resets the scroll position.
    private void EnsureRows(InstalledAppsInfo info)
    {
        string signature = BuildSignature(info.Specs.Apps);

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;
        RebuildRows(info.Specs.Apps);
    }

    private static string BuildSignature(IReadOnlyList<InstalledApp> apps) =>
        apps.Count + "|" + string.Join("|", apps.Select(app =>
            $"{app.Name}:{app.Version}:{(int)app.Scope}"));

    // Rebuilds the detail pane from whichever row is currently highlighted in the main table.
    // Cheap enough (seven rows) to call on every draw rather than tracking whether the selection or
    // the underlying app actually changed.
    private void RefreshDetailPane()
    {
        int index = installedAppsView.SelectedIndex;
        IReadOnlyList<InstalledApp> apps = installedApps?.Specs.Apps ?? [];

        InstalledApp? selected = index >= 0 && index < apps.Count ? apps[index] : null;

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

        installedAppsView.KeyPressed(keyInfo, ref handled);

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
            serviceController.GetService<InstalledAppsService>().RequestImmediateRefresh();
        }
        catch (InvalidOperationException) {
            // No installed-apps service registered (e.g. under test) - nothing to refresh.
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        installedAppsView.BackgroundColour = appConfig.DefaultTheme.Background;
        installedAppsView.ForegroundColour = appConfig.DefaultTheme.Foreground;
        installedAppsView.BorderColour = appConfig.DefaultTheme.ListViewBorder;
        installedAppsView.HeaderBackgroundColour = appConfig.DefaultTheme.HeaderBackground;
        installedAppsView.HeaderForegroundColour = appConfig.DefaultTheme.HeaderForeground;
        installedAppsView.BackgroundHighlightColour = appConfig.DefaultTheme.BackgroundHighlight;
        installedAppsView.ForegroundHighlightColour = appConfig.DefaultTheme.ForegroundHighlight;

        foreach (ListViewColumnHeader columnHeader in installedAppsView.ColumnHeaders) {
            columnHeader.BackgroundColour = appConfig.DefaultTheme.HeaderBackground;
            columnHeader.ForegroundColour = appConfig.DefaultTheme.HeaderForeground;
        }

        detailView.BackgroundColour = appConfig.DefaultTheme.Background;
        detailView.ForegroundColour = appConfig.DefaultTheme.Foreground;
        detailView.BorderColour = appConfig.DefaultTheme.ListViewBorder;
        detailView.HeaderBackgroundColour = appConfig.DefaultTheme.HeaderBackground;
        detailView.HeaderForegroundColour = appConfig.DefaultTheme.HeaderForeground;

        foreach (ListViewColumnHeader columnHeader in detailView.ColumnHeaders) {
            columnHeader.BackgroundColour = appConfig.DefaultTheme.HeaderBackground;
            columnHeader.ForegroundColour = appConfig.DefaultTheme.HeaderForeground;
        }

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnResize()
    {
        int installedAppsViewHeight = Math.Max(1, Height - DetailViewHeight - DetailGutter);

        installedAppsView.X = X;
        installedAppsView.Y = Y;
        installedAppsView.Width = Width;
        installedAppsView.Height = installedAppsViewHeight;

        int fixedWidth = NameColumnWidth + VersionColumnWidth + PublisherColumnWidth
            + ScopeColumnWidth + InstallDateColumnWidth + SizeColumnWidth;

        installedAppsView.ColumnHeaders[0].Width = NameColumnWidth;
        installedAppsView.ColumnHeaders[1].Width = VersionColumnWidth;
        installedAppsView.ColumnHeaders[2].Width = PublisherColumnWidth;
        installedAppsView.ColumnHeaders[3].Width = ScopeColumnWidth;
        installedAppsView.ColumnHeaders[4].Width = InstallDateColumnWidth;
        installedAppsView.ColumnHeaders[5].Width = SizeColumnWidth;
        installedAppsView.ColumnHeaders[6].Width = Math.Max(1, Width - fixedWidth - 3);

        detailView.X = X;
        detailView.Y = Y + installedAppsViewHeight + DetailGutter;
        detailView.Width = Width;
        detailView.Height = DetailViewHeight;

        detailView.ColumnHeaders[0].Width = DetailFieldColumnWidth;
        detailView.ColumnHeaders[1].Width = Math.Max(1, Width - DetailFieldColumnWidth - 3);

        base.OnResize();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        installedAppsView.Items.Clear();
        detailView.Items.Clear();
        lastSignature = string.Empty;

        base.OnUnload();
    }
}
