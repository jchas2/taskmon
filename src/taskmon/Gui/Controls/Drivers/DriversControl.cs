using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Drivers;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.Gui.Controls.Drivers;

// The kernel-mode and file-system drivers registered on this machine - name, status, startup
// type, version and install path - in one selectable, scrolling table. Same shape as
// ServicesControl (same underlying Service Control Manager, just a driver type instead of a
// Win32 one), right down to the DETAIL pane for the columns too narrow to show a long path in
// full.
public sealed partial class DriversControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly ListView driversView;
    private readonly ListView detailView;

    private DriversInfo? drivers;
    private string lastSignature = string.Empty;

    private const int NameColumnWidth = 30;
    private const int VersionColumnWidth = 16;
    private const int StatusColumnWidth = 16;
    private const int StartupTypeColumnWidth = 26;

    private const int DetailFieldColumnWidth = 14;
    private const int DetailViewHeight = 8; // border (2) + column headers (1) + 5 field rows
    private const int DetailGutter = 1;

    public DriversControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;

        driversView = new ListView(terminal) {
            EnableScroll = true,
            EnableRowSelect = true,
            ShowColumnHeaders = true,
            ShowBorder = true,
            TabStop = true,
            TabIndex = 2,
            Visible = true,
            EmptyListViewText = "Gathering drivers…"
        };

        driversView.ColumnHeaders
            .Add(new ListViewColumnHeader("NAME"))
            .Add(new ListViewColumnHeader("VERSION"))
            .Add(new ListViewColumnHeader("STATUS"))
            .Add(new ListViewColumnHeader("START TYPE"))
            .Add(new ListViewColumnHeader("PATH"));

        // The main table's PATH column is too narrow to show a long driver-store path in full;
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

        Controls.Add(driversView);
        Controls.Add(detailView);
    }

    // Wired to the controller event in OnLoad; tests call it directly.
    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Drivers is null) {
            return;
        }

        drivers = snapshot.Drivers;
        Draw();
    }

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();

            if (drivers is { } current) {
                EnsureRows(current);
            }

            RefreshDetailPane();

            driversView.Draw();
            detailView.Draw();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    // Rebuilds the row list only when the set of drivers changes - rather than every publish,
    // since a rebuild resets the scroll position.
    private void EnsureRows(DriversInfo info)
    {
        string signature = BuildSignature(info.Specs.Drivers);

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;
        RebuildRows(info.Specs.Drivers);
    }

    private static string BuildSignature(IReadOnlyList<DriverInfo> entries) =>
        entries.Count + "|" + string.Join("|", entries.Select(driver =>
            $"{driver.ServiceName}:{(int)driver.Status}:{(int)driver.StartType}:{driver.Version}"));

    // Rebuilds the detail pane from whichever row is currently highlighted in the main table.
    // Cheap enough (five rows) to call on every draw rather than tracking whether the selection or
    // the underlying driver actually changed.
    private void RefreshDetailPane()
    {
        int index = driversView.SelectedIndex;
        IReadOnlyList<DriverInfo> entries = drivers?.Specs.Drivers ?? [];

        DriverInfo? selected = index >= 0 && index < entries.Count ? entries[index] : null;

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

        driversView.KeyPressed(keyInfo, ref handled);

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
            serviceController.GetService<DriversService>().RequestImmediateRefresh();
        }
        catch (InvalidOperationException) {
            // No drivers service registered (e.g. under test) - nothing to refresh.
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        driversView.BackgroundColour = appConfig.DefaultTheme.Background;
        driversView.ForegroundColour = appConfig.DefaultTheme.Foreground;
        driversView.BorderColour = appConfig.DefaultTheme.ListViewBorder;
        driversView.HeaderBackgroundColour = appConfig.DefaultTheme.HeaderBackground;
        driversView.HeaderForegroundColour = appConfig.DefaultTheme.HeaderForeground;
        driversView.BackgroundHighlightColour = appConfig.DefaultTheme.BackgroundHighlight;
        driversView.ForegroundHighlightColour = appConfig.DefaultTheme.ForegroundHighlight;

        foreach (ListViewColumnHeader columnHeader in driversView.ColumnHeaders) {
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
        int driversViewHeight = Math.Max(1, Height - DetailViewHeight - DetailGutter);

        driversView.X = X;
        driversView.Y = Y;
        driversView.Width = Width;
        driversView.Height = driversViewHeight;

        int fixedWidth = NameColumnWidth + VersionColumnWidth + StatusColumnWidth + StartupTypeColumnWidth;

        driversView.ColumnHeaders[0].Width = NameColumnWidth;
        driversView.ColumnHeaders[1].Width = VersionColumnWidth;
        driversView.ColumnHeaders[2].Width = StatusColumnWidth;
        driversView.ColumnHeaders[3].Width = StartupTypeColumnWidth;
        driversView.ColumnHeaders[4].Width = Math.Max(1, Width - fixedWidth - 3);

        detailView.X = X;
        detailView.Y = Y + driversViewHeight + DetailGutter;
        detailView.Width = Width;
        detailView.Height = DetailViewHeight;

        detailView.ColumnHeaders[0].Width = DetailFieldColumnWidth;
        detailView.ColumnHeaders[1].Width = Math.Max(1, Width - DetailFieldColumnWidth - 3);

        base.OnResize();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        driversView.Items.Clear();
        detailView.Items.Clear();
        lastSignature = string.Empty;

        base.OnUnload();
    }
}
