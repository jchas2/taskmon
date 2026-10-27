using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.Gui.Controls.Startup;

public sealed partial class StartupControl : Control
{
    private const int NameColumnWidth      = 24;
    private const int PublisherColumnWidth = 24;
    private const int TypeColumnWidth      = 16;
    private const int ScopeColumnWidth     = 9;
    private const int StatusColumnWidth    = 9;

    private const int DetailFieldColumnWidth = 14;
    private const int DetailViewHeight       = 9;

    private const string GatheringText = "Gathering startup applications…";

    private readonly ServiceController        serviceController;
    private readonly AppConfig                appConfig;
    private readonly ListView                 startupView;
    private readonly ListView                 detailView;
    private readonly ListFilter               filter;
    private readonly ListSorter<StartupEntry> sorter;

    private StartupInfo? startup;
    
    private string lastSignature = string.Empty;

    private IReadOnlyList<StartupEntry> visibleEntries = [];


    public StartupControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        startupView = new ListView(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = true,
            TabIndex          = 2,
            Visible           = true,
            EmptyListViewText = GatheringText
        };

        startupView.ColumnHeaders
            .Add(new ListViewColumnHeader("NAME"))
            .Add(new ListViewColumnHeader("PUBLISHER"))
            .Add(new ListViewColumnHeader("TYPE"))
            .Add(new ListViewColumnHeader("SCOPE"))
            .Add(new ListViewColumnHeader("STATUS"))
            .Add(new ListViewColumnHeader("COMMAND"));

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

        filter = new ListFilter(this, terminal, "Filter Startup");
        sorter = new ListSorter<StartupEntry>(terminal, startupView, SortColumns, defaultColumn: 2);

        Controls.Add(startupView);
        Controls.Add(detailView);
        Controls.Add(sorter.SortView);
        Controls.Add(filter.Dialog);
    }

    private static string BuildSignature(IReadOnlyList<StartupEntry> entries) =>
        entries.Count + "|" + string.Join("|", entries.Select(entry =>
            $"{entry.Name}:{(int)entry.Source}:{(int)entry.Scope}:{(int)entry.State}"));

    private void EnsureRows(StartupInfo info)
    {
        string signature =
            BuildSignature(info.Specs.Entries) + "|" + string.Join("|", info.Specs.Notes) + "|" + filter.Text + "|" + sorter.Signature;

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;
        RebuildRows(info.Specs.Entries, info.Specs.Notes);
    }

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    protected override void OnDraw()
    {
        if (startup is { } current) {
            EnsureRows(current);
        }

        RefreshDetailPane();

        sorter.DecorateHeaders();

        startupView.Draw();
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

        startupView.KeyPressed(keyInfo, ref handled);

        if (handled) {
            RefreshDetailPane();
            detailView.Draw();
        }
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(startupView, appConfig.Theme);
        CommonControl.Theme(detailView, appConfig.Theme);
        filter.ApplyTheme(appConfig.Theme);
        sorter.ApplyTheme(appConfig.Theme);

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    protected override void OnResize()
    {
        int startupViewHeight = Math.Max(1, Height - DetailViewHeight);

        startupView.X      = X + sorter.PickerWidth;
        startupView.Y      = Y;
        startupView.Width  = Width - sorter.PickerWidth;
        startupView.Height = startupViewHeight;

        sorter.Layout(X, Y, startupViewHeight);

        int fixedWidth = 
            NameColumnWidth      + 
            PublisherColumnWidth + 
            TypeColumnWidth      + 
            ScopeColumnWidth     + 
            StatusColumnWidth;

        startupView.ColumnHeaders[0].Width = NameColumnWidth;
        startupView.ColumnHeaders[1].Width = PublisherColumnWidth;
        startupView.ColumnHeaders[2].Width = TypeColumnWidth;
        startupView.ColumnHeaders[3].Width = ScopeColumnWidth;
        startupView.ColumnHeaders[4].Width = StatusColumnWidth;
        startupView.ColumnHeaders[5].Width = Math.Max(1, startupView.Width - fixedWidth - 3);

        detailView.X      = X;
        detailView.Y      = Y + startupViewHeight;
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
        startupView.Items.Clear();
        detailView.Items.Clear();
        lastSignature = string.Empty;
        filter.Close();

        base.OnUnload();
    }

    private void RefreshDetailPane()
    {
        int index = startupView.SelectedIndex;
        
        IReadOnlyList<StartupEntry> entries = visibleEntries;

        StartupEntry? selected = index >= 0 && index < entries.Count ? entries[index] : null;

        RebuildDetailRows(selected);
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Startup is null) {
            return;
        }

        startup = snapshot.Startup;
        Draw();
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
}
