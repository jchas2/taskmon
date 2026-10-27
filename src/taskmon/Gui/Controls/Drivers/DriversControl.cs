using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Drivers;

namespace Task.Monitor.Gui.Controls.Drivers;

public sealed partial class DriversControl : Control
{
    private const int NameColumnWidth        = 30;
    private const int VersionColumnWidth     = 16;
    private const int StatusColumnWidth      = 16;
    private const int StartupTypeColumnWidth = 26;

    private const int DetailFieldColumnWidth = 14;
    private const int DetailViewHeight       = 9;   // border (2) + column headers (1) + 6 field rows

    private const string GatheringText = "Gathering drivers…";

    private readonly ServiceController      serviceController;
    private readonly AppConfig              appConfig;
    private readonly ListView               driversView;
    private readonly ListView               detailView;
    private readonly ListFilter             filter;
    private readonly ListSorter<DriverInfo> sorter;

    private DriversInfo? drivers;
    
    private string lastSignature = string.Empty;

    private IReadOnlyList<DriverInfo> visibleDrivers = [];


    public DriversControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig) 
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;

        driversView = new ListView(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = true,
            TabIndex          = 2,
            Visible           = true,
            EmptyListViewText = GatheringText
        };

        driversView.ColumnHeaders
            .Add(new ListViewColumnHeader("NAME"))
            .Add(new ListViewColumnHeader("VERSION"))
            .Add(new ListViewColumnHeader("STATUS"))
            .Add(new ListViewColumnHeader("START TYPE"))
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

        filter = new ListFilter(this, terminal, "Filter Drivers");
        sorter = new ListSorter<DriverInfo>(terminal, driversView, SortColumns, defaultColumn: 0);

        Controls.Add(driversView);
        Controls.Add(detailView);
        Controls.Add(sorter.SortView);
        Controls.Add(filter.Dialog);
    }

    private static string BuildSignature(IReadOnlyList<DriverInfo> entries) =>
        entries.Count + "|" + 
        string.Join("|", entries.Select(driver =>
            $"{driver.Name}:{(int)driver.Status}:{(int)driver.StartType}:{driver.Version}"));

    private void EnsureRows(DriversInfo info)
    {
        string signature = BuildSignature(info.Specs.Drivers) + "|" + filter.Text + "|" + sorter.Signature;

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;
        RebuildRows(info.Specs.Drivers);
    }

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    protected override void OnDraw()
    {
        if (drivers is { } current) {
            EnsureRows(current);
        }

        RefreshDetailPane();

        sorter.DecorateHeaders();

        driversView.Draw();
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

        driversView.KeyPressed(keyInfo, ref handled);

        if (handled) {
            RefreshDetailPane();
            detailView.Draw();
        }
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(driversView, appConfig.Theme);
        CommonControl.Theme(detailView,  appConfig.Theme);
        filter.ApplyTheme(appConfig.Theme);
        sorter.ApplyTheme(appConfig.Theme);
        
        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    protected override void OnResize()
    {
        int driversViewHeight = Math.Max(1, Height - DetailViewHeight);

        driversView.X      = X + sorter.PickerWidth;
        driversView.Y      = Y;
        driversView.Width  = Width - sorter.PickerWidth;
        driversView.Height = driversViewHeight;

        sorter.Layout(X, Y, driversViewHeight);

        int fixedWidth = NameColumnWidth + VersionColumnWidth + StatusColumnWidth + StartupTypeColumnWidth;

        driversView.ColumnHeaders[0].Width = NameColumnWidth;
        driversView.ColumnHeaders[1].Width = VersionColumnWidth;
        driversView.ColumnHeaders[2].Width = StatusColumnWidth;
        driversView.ColumnHeaders[3].Width = StartupTypeColumnWidth;
        driversView.ColumnHeaders[4].Width = Math.Max(1, driversView.Width - fixedWidth - 3);

        detailView.X      = X;
        detailView.Y      = Y + driversViewHeight;
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
        driversView.Items.Clear();
        detailView.Items.Clear();
        lastSignature = string.Empty;
        filter.Close();

        base.OnUnload();
    }

    private void RefreshDetailPane()
    {
        int index = driversView.SelectedIndex;

        IReadOnlyList<DriverInfo> entries = visibleDrivers;
        DriverInfo? selected = index >= 0 && index < entries.Count ? entries[index] : null;
        
        RebuildDetailRows(selected);
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Drivers is null) {
            return;
        }

        drivers = snapshot.Drivers;
        Draw();
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
}
