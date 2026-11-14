using Task.Monitor.Configuration;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.InstalledApps;

namespace Task.Monitor.Gui.Controls.InstalledApps;

public sealed partial class InstalledAppsControl : Control
{
    private const int NameColumnWidth        = 30;
    private const int VersionColumnWidth     = 14;
    private const int PublisherColumnWidth   = 24;
    private const int ScopeColumnWidth       = 9;
    private const int InstallDateColumnWidth = 12;
    private const int SizeColumnWidth        = 10;

    private const int DetailFieldColumnWidth = 14;
    private const int DetailViewHeight       = 10; // border (2) + column headers (1) + 7 field rows

    private readonly ServiceController        serviceController;
    private readonly AppConfig                appConfig;
    private readonly ListView                 installedAppsView;
    private readonly ListView                 detailView;
    private readonly ListFilter               filter;
    private readonly ListSorter<InstalledApp> sorter;

    private InstalledAppsInfo? installedApps;
    
    private string lastSignature = string.Empty;

    private IReadOnlyList<InstalledApp> visibleApps = [];


    public InstalledAppsControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        installedAppsView = new ListView(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = true,
            TabIndex          = 2,
            Visible           = true,
            EmptyListViewText = GatheringText
        };

        installedAppsView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.ListColumnName))
            .Add(new ListViewColumnHeader(Strings.ListColumnVersion))
            .Add(new ListViewColumnHeader(Strings.ListColumnPublisher))
            .Add(new ListViewColumnHeader(Strings.ListColumnScope))
            .Add(new ListViewColumnHeader(Strings.AppsColumnInstalled))
            .Add(new ListViewColumnHeader(Strings.ListColumnSize))
            .Add(new ListViewColumnHeader(Strings.AppsColumnLocation));

        detailView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = false,
            Visible           = true,
            HeaderText        = Strings.ListDetailHeader
        };

        detailView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.ListColumnField))
            .Add(new ListViewColumnHeader(Strings.ListColumnValue));

        filter = new ListFilter(this, terminal, Strings.AppsFilterTitle);
        sorter = new ListSorter<InstalledApp>(terminal, installedAppsView, SortColumns, defaultColumn: 0);

        Controls.Add(installedAppsView);
        Controls.Add(detailView);
        Controls.Add(sorter.SortView);
        Controls.Add(filter.Dialog);
    }

    private static string BuildSignature(IReadOnlyList<InstalledApp> apps) =>
        apps.Count + "|" + 
        string.Join("|", apps.Select(app =>
            $"{app.Name}:{app.Version}:{(int)app.Scope}:{app.EstimatedSizeKb}:{app.SizePending}"));

    private void EnsureRows(InstalledAppsInfo info)
    {
        string signature = BuildSignature(info.Specs.Apps) + "|" + filter.Text + "|" + sorter.Signature;

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;
        
        RebuildRows(info.Specs.Apps);
    }

    private static string GatheringText => Strings.AppsGathering;

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    protected override void OnDraw()
    {
        if (installedApps is { } current) {
            EnsureRows(current);
        }

        RefreshDetailPane();

        sorter.DecorateHeaders();

        installedAppsView.Draw();
        detailView.Draw();
        sorter.Draw();
        filter.Draw();
    }

    protected override void OnGotFocus() => sorter.FocusTarget.SetFocus();

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (!filter.IsDialogVisible && sorter.KeyPressed(keyInfo, ref handled)) {
            Clear();
            Resize();
            Draw();
        }

        if (handled) {
            return;
        }

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

        installedAppsView.KeyPressed(keyInfo, ref handled);

        if (handled) {
            RefreshDetailPane();
            detailView.Draw();
        }
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(installedAppsView, appConfig.Theme);
        CommonControl.Theme(detailView, appConfig.Theme);
        filter.ApplyTheme(appConfig.Theme);
        sorter.ApplyTheme(appConfig.Theme);
        
        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    protected override void OnResize()
    {
        int installedAppsViewHeight = Math.Max(1, Height - DetailViewHeight);

        installedAppsView.X      = X + sorter.PickerWidth;
        installedAppsView.Y      = Y;
        installedAppsView.Width  = Width - sorter.PickerWidth;
        installedAppsView.Height = installedAppsViewHeight;

        sorter.Layout(X, Y, installedAppsViewHeight);

        int fixedWidth = 
            NameColumnWidth + 
            VersionColumnWidth + 
            PublisherColumnWidth + 
            ScopeColumnWidth + 
            InstallDateColumnWidth + 
            SizeColumnWidth;

        installedAppsView.ColumnHeaders[0].Width = NameColumnWidth;
        installedAppsView.ColumnHeaders[1].Width = VersionColumnWidth;
        installedAppsView.ColumnHeaders[2].Width = PublisherColumnWidth;
        installedAppsView.ColumnHeaders[3].Width = ScopeColumnWidth;
        installedAppsView.ColumnHeaders[4].Width = InstallDateColumnWidth;
        installedAppsView.ColumnHeaders[5].Width = SizeColumnWidth;
        installedAppsView.ColumnHeaders[6].Width = Math.Max(1, installedAppsView.Width - fixedWidth - 3);

        installedAppsView.ColumnHeaders[1].RightAligned = true;
        installedAppsView.ColumnHeaders[5].RightAligned = true;

        detailView.X      = X;
        detailView.Y      = Y + installedAppsViewHeight;
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
        installedAppsView.Items.Clear();
        detailView.Items.Clear();
        lastSignature = string.Empty;
        filter.Close();

        base.OnUnload();
    }

    private void RefreshDetailPane()
    {
        int index = installedAppsView.SelectedIndex;
        
        IReadOnlyList<InstalledApp> apps     = visibleApps;
        InstalledApp?               selected = index >= 0 && index < apps.Count ? apps[index] : null;
        
        RebuildDetailRows(selected);
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.InstalledApps is null) {
            return;
        }

        installedApps = snapshot.InstalledApps;
        Draw();
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
}
