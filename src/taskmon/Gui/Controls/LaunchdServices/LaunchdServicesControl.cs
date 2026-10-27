using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.LaunchdServices;

namespace Task.Monitor.Gui.Controls.LaunchdServices;

public sealed partial class LaunchdServicesControl : Control
{
    private const int LabelColumnWidth     = 44;
    private const int GroupColumnWidth     = 32;
    private const int StatusColumnWidth    = 18;
    private const int StartColumnWidth     = 12;
    private const int EnabledColumnWidth   = 9;

    private const int DetailFieldColumnWidth = 14;
    private const int DetailViewHeight       = 14;  // border (2) + column headers (1) + 11 field rows

    private const string GatheringText = "Gathering launchd services…";

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly ListView          servicesView;
    private readonly ListView          detailView;
    private readonly ListFilter        filter;

    private LaunchdServicesInfo? services;

    private string lastSignature = string.Empty;

    private IReadOnlyList<LaunchdServiceInfo> visibleServices = [];

    public LaunchdServicesControl(
        ServiceController serviceController,
        ISystemTerminal   terminal,
        AppConfig         appConfig) : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        servicesView = new ListView(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = true,
            TabIndex          = 2,
            Visible           = true,
            EmptyListViewText = GatheringText
        };

        servicesView.ColumnHeaders
            .Add(new ListViewColumnHeader("LABEL"))
            .Add(new ListViewColumnHeader("GROUP"))
            .Add(new ListViewColumnHeader("STATUS"))
            .Add(new ListViewColumnHeader("START"))
            .Add(new ListViewColumnHeader("ENABLED"))
            .Add(new ListViewColumnHeader("PATH"));

        detailView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = false,
            Visible           = true,
            HeaderText        = "DETAIL"
        };

        detailView.ColumnHeaders
            .Add(new ListViewColumnHeader("FIELD"))
            .Add(new ListViewColumnHeader("VALUE"));

        filter = new ListFilter(this, terminal, "Filter Services");

        Controls.Add(servicesView);
        Controls.Add(detailView);
        Controls.Add(filter.Dialog);
    }

    private static string BuildSignature(IReadOnlyList<LaunchdServiceInfo> entries) =>
        entries.Count + "|" +
        string.Join("|", entries.Select(service =>
            $"{service.Domain}:{service.Label}:{(int)service.Status}:{service.Pid}:{service.Disabled}:{service.LastExitStatus}"));

    private void EnsureRows(LaunchdServicesInfo info)
    {
        string signature = BuildSignature(info.Specs.Services) + "|" + filter.Text;

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;
        RebuildRows(info.Specs.Services);
    }

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    protected override void OnDraw()
    {
        if (services is { } current) {
            EnsureRows(current);
        }

        RefreshDetailPane();

        servicesView.Draw();
        detailView.Draw();
        filter.Draw();
    }

    protected override void OnGotFocus() => servicesView.SetFocus();

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (filter.KeyPressed(keyInfo, ref handled)) {
            Draw();
        }

        if (handled) {
            return;
        }

        switch (keyInfo.Key) {
            case ConsoleKey.R:
            case ConsoleKey.F5:
                TryRequestRefresh();
                handled = true;
                return;
        }

        servicesView.KeyPressed(keyInfo, ref handled);

        if (handled) {
            RefreshDetailPane();
            detailView.Draw();
        }
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(servicesView, appConfig.Theme);
        CommonControl.Theme(detailView,   appConfig.Theme);
        filter.ApplyTheme(appConfig.Theme);

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    protected override void OnResize()
    {
        int servicesViewHeight = Math.Max(1, Height - DetailViewHeight);

        servicesView.X      = X;
        servicesView.Y      = Y;
        servicesView.Width  = Width;
        servicesView.Height = servicesViewHeight;

        int fixedWidth = LabelColumnWidth + GroupColumnWidth + StatusColumnWidth + StartColumnWidth + EnabledColumnWidth;

        servicesView.ColumnHeaders[0].Width = LabelColumnWidth;
        servicesView.ColumnHeaders[1].Width = GroupColumnWidth;
        servicesView.ColumnHeaders[2].Width = StatusColumnWidth;
        servicesView.ColumnHeaders[3].Width = StartColumnWidth;
        servicesView.ColumnHeaders[4].Width = EnabledColumnWidth;
        servicesView.ColumnHeaders[5].Width = Math.Max(1, Width - fixedWidth - 3);

        detailView.X      = X;
        detailView.Y      = Y + servicesViewHeight;
        detailView.Width  = Width;
        detailView.Height = DetailViewHeight;

        detailView.ColumnHeaders[0].Width = DetailFieldColumnWidth;
        detailView.ColumnHeaders[1].Width = Math.Max(1, Width - DetailFieldColumnWidth - 3);

        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        servicesView.Items.Clear();
        detailView.Items.Clear();
        lastSignature = string.Empty;
        filter.Close();

        base.OnUnload();
    }

    private void RefreshDetailPane()
    {
        int index = servicesView.SelectedIndex;

        IReadOnlyList<LaunchdServiceInfo> entries = visibleServices;
        LaunchdServiceInfo? selected = index >= 0 && index < entries.Count ? entries[index] : null;

        RebuildDetailRows(selected);
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.LaunchdServices is null) {
            return;
        }

        services = snapshot.LaunchdServices;
        Draw();
    }

    private void TryRequestRefresh()
    {
        try {
            serviceController.GetService<LaunchdServicesService>().RequestImmediateRefresh();
        }
        catch (InvalidOperationException) {
            // No launchd services service registered (e.g. under test) - nothing to refresh.
        }
    }
}
