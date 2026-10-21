using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.Gui.Controls.Services;

public sealed partial class ServicesControl : Control
{
    private const int NameColumnWidth        = 30;
    private const int StatusColumnWidth      = 16;
    private const int StartupTypeColumnWidth = 26;
    private const int LogOnAsColumnWidth     = 20;

    private const int DetailFieldColumnWidth = 14;
    private const int DetailViewHeight       = 8; 

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly ListView          servicesView;
    private readonly ListView          detailView;

    private WindowsServicesInfo? services;
    
    private string lastSignature = string.Empty;


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
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = true,
            TabIndex          = 2,
            Visible           = true,
            EmptyListViewText = "Gathering Windows services…"
        };

        servicesView.ColumnHeaders
            .Add(new ListViewColumnHeader("NAME"))
            .Add(new ListViewColumnHeader("STATUS"))
            .Add(new ListViewColumnHeader("STARTUP TYPE"))
            .Add(new ListViewColumnHeader("LOG ON AS"))
            .Add(new ListViewColumnHeader("DESCRIPTION"));

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

        Controls.Add(servicesView);
        Controls.Add(detailView);
    }

    private static string BuildSignature(IReadOnlyList<WindowsServiceInfo> entries) =>
        entries.Count + "|" + 
        string.Join("|", entries.Select(service =>
            $"{service.ServiceName}:{(int)service.Status}:{(int)service.StartType}:{service.DelayedAutoStart}"));

    private void EnsureRows(WindowsServicesInfo info)
    {
        string signature = BuildSignature(info.Specs.Services);

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
    }

    protected override void OnGotFocus() => servicesView.SetFocus();

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

        int fixedWidth = NameColumnWidth + StatusColumnWidth + StartupTypeColumnWidth + LogOnAsColumnWidth;

        servicesView.ColumnHeaders[0].Width = NameColumnWidth;
        servicesView.ColumnHeaders[1].Width = StatusColumnWidth;
        servicesView.ColumnHeaders[2].Width = StartupTypeColumnWidth;
        servicesView.ColumnHeaders[3].Width = LogOnAsColumnWidth;
        servicesView.ColumnHeaders[4].Width = Math.Max(1, Width - fixedWidth - 3);

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

        base.OnUnload();
    }

    private void RefreshDetailPane()
    {
        int index = servicesView.SelectedIndex;
        
        IReadOnlyList<WindowsServiceInfo> entries = services?.Specs.Services ?? [];

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
