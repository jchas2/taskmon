using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Drivers;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.Gui.Controls.Drivers;

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

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Drivers is null) {
            return;
        }

        drivers = snapshot.Drivers;
        Draw();
    }

    protected override void OnGotFocus() => driversView.SetFocus();

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    protected override void OnDraw()
    {
        if (drivers is { } current) {
            EnsureRows(current);
        }

        RefreshDetailPane();

        driversView.Draw();
        detailView.Draw();
    }

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
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

        driversView.BackgroundColour = appConfig.Theme.ListViewBackground;
        driversView.ForegroundColour = appConfig.Theme.ListViewForeground;
        driversView.BorderForegroundColour = appConfig.Theme.ListViewBorderForeground;
        driversView.BorderBackgroundColour = appConfig.Theme.ListViewBorderBackground;
        driversView.HeaderBackgroundColour = appConfig.Theme.HeaderBackground;
        driversView.HeaderForegroundColour = appConfig.Theme.HeaderForeground;
        driversView.BackgroundHighlightColour = appConfig.Theme.BackgroundHighlight;
        driversView.ForegroundHighlightColour = appConfig.Theme.ForegroundHighlight;
        driversView.BackgroundHighlightInactiveColour = appConfig.Theme.BackgroundHighlightInactive;
        driversView.ForegroundHighlightInactiveColour = appConfig.Theme.ForegroundHighlightInactive;

        foreach (ListViewColumnHeader columnHeader in driversView.ColumnHeaders) {
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
        int driversViewHeight = Math.Max(1, Height - DetailViewHeight);

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
        detailView.Y = Y + driversViewHeight;
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
