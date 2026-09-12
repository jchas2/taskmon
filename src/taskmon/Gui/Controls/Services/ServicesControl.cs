using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.Gui.Controls.Services;

// The Windows services registered on this machine - the same inventory the Services snap-in
// (services.msc) shows - in one selectable, scrolling table: name, status, startup type, the
// account it logs on as, and its description.
public sealed partial class ServicesControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly ListView servicesView;
    private readonly ListView detailView;

    private WindowsServicesInfo? services;
    private string lastSignature = string.Empty;

    private const int NameColumnWidth = 30;
    private const int StatusColumnWidth = 16;
    private const int StartupTypeColumnWidth = 26;
    private const int LogOnAsColumnWidth = 20;

    private const int DetailFieldColumnWidth = 14;
    private const int DetailViewHeight = 8; // border (2) + column headers (1) + 5 field rows
    private const int DetailGutter = 1;

    public ServicesControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;

        servicesView = new ListView(terminal) {
            EnableScroll = true,
            EnableRowSelect = true,
            ShowColumnHeaders = true,
            ShowBorder = true,
            TabStop = true,
            TabIndex = 2,
            Visible = true,
            EmptyListViewText = "Gathering Windows services…",
            HeaderText = "WINDOWS SERVICES"
        };

        servicesView.ColumnHeaders
            .Add(new ListViewColumnHeader("NAME"))
            .Add(new ListViewColumnHeader("STATUS"))
            .Add(new ListViewColumnHeader("STARTUP TYPE"))
            .Add(new ListViewColumnHeader("LOG ON AS"))
            .Add(new ListViewColumnHeader("DESCRIPTION"));

        // The main table's columns are too narrow to show a long description in full; this mirrors
        // the selected row's fields one per line so every value can be read.
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

        Controls.Add(servicesView);
        Controls.Add(detailView);
    }

    // Wired to the controller event in OnLoad; tests call it directly.
    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.WindowsServices is null) {
            return;
        }

        services = snapshot.WindowsServices;
        Draw();
    }

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();

            if (services is { } current) {
                EnsureRows(current);
            }

            RefreshDetailPane();

            servicesView.Draw();
            detailView.Draw();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    // Rebuilds the row list only when the set of services changes - rather than every publish,
    // since a rebuild resets the scroll position.
    private void EnsureRows(WindowsServicesInfo info)
    {
        string signature = BuildSignature(info.Specs.Services);

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;
        RebuildRows(info.Specs.Services);
    }

    private static string BuildSignature(IReadOnlyList<WindowsServiceInfo> entries) =>
        entries.Count + "|" + string.Join("|", entries.Select(service =>
            $"{service.ServiceName}:{(int)service.Status}:{(int)service.StartType}:{service.DelayedAutoStart}"));

    // Rebuilds the detail pane from whichever row is currently highlighted in the main table.
    // Cheap enough (five rows) to call on every draw rather than tracking whether the selection or
    // the underlying service actually changed.
    private void RefreshDetailPane()
    {
        int index = servicesView.SelectedIndex;
        IReadOnlyList<WindowsServiceInfo> entries = services?.Specs.Services ?? [];

        WindowsServiceInfo? selected = index >= 0 && index < entries.Count ? entries[index] : null;

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

        servicesView.KeyPressed(keyInfo, ref handled);

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
            serviceController.GetService<WindowsServicesService>().RequestImmediateRefresh();
        }
        catch (InvalidOperationException) {
            // No services service registered (e.g. under test) - nothing to refresh.
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        servicesView.BackgroundColour = appConfig.DefaultTheme.Background;
        servicesView.ForegroundColour = appConfig.DefaultTheme.Foreground;
        servicesView.BorderColour = appConfig.DefaultTheme.ListViewBorder;
        servicesView.HeaderBackgroundColour = appConfig.DefaultTheme.HeaderBackground;
        servicesView.HeaderForegroundColour = appConfig.DefaultTheme.HeaderForeground;
        servicesView.BackgroundHighlightColour = appConfig.DefaultTheme.BackgroundHighlight;
        servicesView.ForegroundHighlightColour = appConfig.DefaultTheme.ForegroundHighlight;

        foreach (ListViewColumnHeader columnHeader in servicesView.ColumnHeaders) {
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
        int servicesViewHeight = Math.Max(1, Height - DetailViewHeight - DetailGutter);

        servicesView.X = X;
        servicesView.Y = Y;
        servicesView.Width = Width;
        servicesView.Height = servicesViewHeight;

        int fixedWidth = NameColumnWidth + StatusColumnWidth + StartupTypeColumnWidth + LogOnAsColumnWidth;

        servicesView.ColumnHeaders[0].Width = NameColumnWidth;
        servicesView.ColumnHeaders[1].Width = StatusColumnWidth;
        servicesView.ColumnHeaders[2].Width = StartupTypeColumnWidth;
        servicesView.ColumnHeaders[3].Width = LogOnAsColumnWidth;
        servicesView.ColumnHeaders[4].Width = Math.Max(1, Width - fixedWidth - 3);

        detailView.X = X;
        detailView.Y = Y + servicesViewHeight + DetailGutter;
        detailView.Width = Width;
        detailView.Height = DetailViewHeight;

        detailView.ColumnHeaders[0].Width = DetailFieldColumnWidth;
        detailView.ColumnHeaders[1].Width = Math.Max(1, Width - DetailFieldColumnWidth - 3);

        base.OnResize();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        servicesView.Items.Clear();
        detailView.Items.Clear();
        lastSignature = string.Empty;

        base.OnUnload();
    }
}
