using Task.Monitor.Configuration;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.WindowsServices;
using MessageBox = Task.Monitor.System.Controls.MessageBox.MessageBox;

namespace Task.Monitor.Gui.Controls.Services;

public sealed partial class ServicesControl : Control
{
    private const int NameColumnWidth        = 30;
    private const int StatusColumnWidth      = 17;
    private const int StartupTypeColumnWidth = 26;
    private const int LogOnAsColumnWidth     = 20;

    private const int DetailFieldColumnWidth = 14;
    private const int DetailViewHeight       = 8; 

    private readonly ServiceController              serviceController;
    private readonly AppConfig                      appConfig;
    private readonly ListView                       servicesView;
    private readonly ListView                       detailView;
    private readonly ListFilter                     filter;
    private readonly ListSorter<WindowsServiceInfo> sorter;
    private readonly MessageBox                     toggleDialog;

    private WindowsServicesInfo? services;

    private bool loaded;
    
    private string lastSignature = string.Empty;

    private IReadOnlyList<WindowsServiceInfo> visibleServices = [];


    public ServicesControl(
        ServiceController serviceController,
        ISystemTerminal   terminal,
        AppConfig         appConfig) : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        servicesView = new ListView(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowCheckboxes    = WindowsServicesService.SupportsControl,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = true,
            TabIndex          = 2,
            Visible           = true,
            EmptyListViewText = GatheringText
        };

        servicesView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.ListColumnName))
            .Add(new ListViewColumnHeader(Strings.ListColumnStatus))
            .Add(new ListViewColumnHeader(Strings.ServicesColumnStartupType))
            .Add(new ListViewColumnHeader(Strings.ServicesColumnLogOnAs))
            .Add(new ListViewColumnHeader(Strings.ListColumnDescription));

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

        filter = new ListFilter(this, terminal, Strings.ServicesFilterTitle);
        sorter = new ListSorter<WindowsServiceInfo>(terminal, servicesView, SortColumns, defaultColumn: 0);

        toggleDialog = new MessageBox(terminal) {
            Visible = false,
            Height  = MessageBox.PreferredHeight
        };

        CommonControl.Text(toggleDialog);

        Controls.Add(servicesView);
        Controls.Add(detailView);
        Controls.Add(sorter.SortView);
        Controls.Add(filter.Dialog);
        Controls.Add(toggleDialog);

        RequestControl = RequestControlThroughService;
    }

    private static string BuildSignature(IReadOnlyList<WindowsServiceInfo> entries) =>
        entries.Count + "|" +
        string.Join("|", entries.Select(service =>
            $"{service.ServiceName}:{(int)service.Status}:{(int)service.StartType}:{service.DelayedAutoStart}"));

    private void EnsureRows(WindowsServicesInfo info)
    {
        string signature =
            BuildSignature(info.Specs.Services) + "|" +
            string.Join(",", inFlight.Select(request => $"{request.Key}:{request.Value}")) + "|" +
            filter.Text + "|" +
            sorter.Signature;

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;
        RebuildRows(info.Specs.Services);
    }

    private static string GatheringText => Strings.ServicesGathering;

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    protected override void OnDraw()
    {
        if (services is { } current) {
            EnsureRows(current);
        }

        RefreshDetailPane();

        sorter.DecorateHeaders();

        servicesView.Draw();
        detailView.Draw();
        sorter.Draw();
        filter.Draw();

        if (toggleDialog.Visible) {
            toggleDialog.Draw();
        }

        ShowPendingFailure();
    }

    protected override void OnGotFocus() => sorter.FocusTarget.SetFocus();

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (toggleDialog.Visible) {
            OnToggleDialogKeyPressed(keyInfo, ref handled);
            return;
        }

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

            case ConsoleKey.Spacebar when CanControl:
                if (!sorter.IsSelectionActive) {
                    ToggleSelectedService();
                }

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
        CommonControl.Theme(detailView, appConfig.Theme);
        CommonControl.Theme(toggleDialog, appConfig.Theme);
        filter.ApplyTheme(appConfig.Theme);
        sorter.ApplyTheme(appConfig.Theme);

        lock (DrawingLock) {
            loaded = true;
        }

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    protected override void OnResize()
    {
        int servicesViewHeight = Math.Max(1, Height - DetailViewHeight);

        servicesView.X      = X + sorter.PickerWidth;
        servicesView.Y      = Y;
        servicesView.Width  = Width - sorter.PickerWidth;
        servicesView.Height = servicesViewHeight;

        sorter.Layout(X, Y, servicesViewHeight);

        int fixedWidth =
            (servicesView.ShowCheckboxes ? ListView.CheckboxWidth : 0) +
            NameColumnWidth                                           +
            StatusColumnWidth                                         +
            StartupTypeColumnWidth                                    +
            LogOnAsColumnWidth;

        servicesView.ColumnHeaders[0].Width = NameColumnWidth;
        servicesView.ColumnHeaders[1].Width = StatusColumnWidth;
        servicesView.ColumnHeaders[2].Width = StartupTypeColumnWidth;
        servicesView.ColumnHeaders[3].Width = LogOnAsColumnWidth;
        servicesView.ColumnHeaders[4].Width = Math.Max(1, servicesView.Width - fixedWidth - 3);

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

        lock (DrawingLock) {
            loaded         = false;
            pendingFailure = null;
            inFlight.Clear();
        }

        servicesView.Items.Clear();
        detailView.Items.Clear();
        lastSignature = string.Empty;
        filter.Close();
        CloseToggleDialog();

        base.OnUnload();
    }

    private void RefreshDetailPane()
    {
        int index = servicesView.SelectedIndex;
        
        IReadOnlyList<WindowsServiceInfo> entries = visibleServices;

        WindowsServiceInfo? selected = index >= 0 && index < entries.Count 
            ? entries[index] 
            : null;

        RebuildDetailRows(selected);
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.WindowsServices is null) {
            return;
        }

        services = snapshot.WindowsServices;
        Draw();
    }

    internal ListView ServicesView => servicesView;

    private void TryRequestRefresh()
    {
        try {
            serviceController.GetService<WindowsServicesService>().RequestImmediateRefresh();
        }
        catch (InvalidOperationException) {
            // No services service registered (e.g. under test) - nothing to refresh.
        }
    }
}
