using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.InstalledApps;

namespace Task.Monitor.Gui.Controls.InstalledApps;

// The installed applications in one selectable, scrolling table: on Windows, those registered
// under the Uninstall registry keys (the source Settings > Apps reads); on macOS, the app bundles in
// /Applications, /System/Applications and ~/Applications.
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

    private const string GatheringText = "Gathering installed applications…";
    
    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly ListView          installedAppsView;
    private readonly ListView          detailView;
    private readonly ListFilter        filter;

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
            .Add(new ListViewColumnHeader("NAME"))
            .Add(new ListViewColumnHeader("VERSION"))
            .Add(new ListViewColumnHeader("PUBLISHER"))
            .Add(new ListViewColumnHeader("SCOPE"))
            .Add(new ListViewColumnHeader("INSTALLED"))
            .Add(new ListViewColumnHeader("SIZE"))
            .Add(new ListViewColumnHeader("LOCATION"));

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

        filter = new ListFilter(this, terminal, "Filter Apps");

        Controls.Add(installedAppsView);
        Controls.Add(detailView);
        Controls.Add(filter.Dialog);
    }

    // Includes sizes, which on macOS arrive after the rows as each bundle is measured.
    private static string BuildSignature(IReadOnlyList<InstalledApp> apps) =>
        apps.Count + "|" + 
        string.Join("|", apps.Select(app =>
            $"{app.Name}:{app.Version}:{(int)app.Scope}:{app.EstimatedSizeKb}:{app.SizePending}"));

    private void EnsureRows(InstalledAppsInfo info)
    {
        string signature = BuildSignature(info.Specs.Apps) + "|" + filter.Text;

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;
        
        RebuildRows(info.Specs.Apps);
    }

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    protected override void OnDraw()
    {
        if (installedApps is { } current) {
            EnsureRows(current);
        }

        RefreshDetailPane();

        installedAppsView.Draw();
        detailView.Draw();
        filter.Draw();
    }

    protected override void OnGotFocus() => installedAppsView.SetFocus();

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
        
        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    protected override void OnResize()
    {
        int installedAppsViewHeight = Math.Max(1, Height - DetailViewHeight);

        installedAppsView.X      = X;
        installedAppsView.Y      = Y;
        installedAppsView.Width  = Width;
        installedAppsView.Height = installedAppsViewHeight;

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
        installedAppsView.ColumnHeaders[6].Width = Math.Max(1, Width - fixedWidth - 3);

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
