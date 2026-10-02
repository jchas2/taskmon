using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.SystemInformation;

public sealed partial class SystemInfoControl : Control
{
    private enum Section { Cpu, Memory, Gpu, Disk, Network }

    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly MenuControl navMenu;
    private readonly ListView systemInfoView;
    private readonly SystemLogoControl logoControl;
    private readonly ListView systemSummaryView;

    private SystemSnapshot? snapshot;

    private string lastSectionSignature = string.Empty;
    private string lastSummarySignature = string.Empty;
    private Section selectedSection = Section.Cpu;

    private const int LabelColumnWidth = 32;
    private const int NavWidth = 12;
    private const string GatheringText = "Gathering…";

    public SystemInfoControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;

        navMenu = new MenuControl(terminal, appConfig) {
            TabStop = true,
            TabIndex = 1,
            Visible = true
        };

        systemInfoView = new ListView(terminal) {
            EnableScroll = true,
            EnableRowSelect = true,
            ShowColumnHeaders = false,
            ShowBorder = true,
            TabStop = true,
            TabIndex = 2,
            Visible = true,
            EmptyListViewText = "Gathering system information…",
            FooterText = "↑ ↓ PgUp PgDn Scroll"
        };

        systemInfoView.ColumnHeaders
            .Add(new ListViewColumnHeader(string.Empty))
            .Add(new ListViewColumnHeader(string.Empty));

        logoControl = new SystemLogoControl(terminal) {
            Visible = true
        };

        systemSummaryView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowColumnHeaders = false,
            ShowBorder = false,
            TabStop = false,
            Visible = true,
            EmptyListViewText = "Gathering system information…"
        };

        systemSummaryView.ColumnHeaders
            .Add(new ListViewColumnHeader(string.Empty))
            .Add(new ListViewColumnHeader(string.Empty));

        Controls
            .Add(logoControl)
            .Add(systemSummaryView)
            .Add(navMenu)
            .Add(systemInfoView);
    }

    protected override void OnGotFocus() => navMenu.SetFocus();

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    public void Sample(SystemSnapshot snapshot)
    {
        this.snapshot = snapshot;
        Draw();
    }

    protected override void OnDraw()
    {
        SystemSnapshot s = snapshot ?? new SystemSnapshot();

        EnsureSummaryRows(s);
        EnsureSectionRows(s);

        logoControl.Draw();
        systemSummaryView.Draw();
        navMenu.Draw();
        systemInfoView.Draw();
    }

    private void EnsureSummaryRows(SystemSnapshot snapshot)
    {
        string signature = BuildSummarySignature(snapshot);

        if (signature == lastSummarySignature) {
            return;
        }

        lastSummarySignature = signature;
        RebuildSummaryRows(snapshot);
    }

    private void EnsureSectionRows(SystemSnapshot snapshot)
    {
        string signature = BuildSectionSignature(snapshot);

        if (signature == lastSectionSignature) {
            return;
        }

        lastSectionSignature = signature;
        RebuildRows(snapshot);
    }

    private string BuildSummarySignature(SystemSnapshot s) =>
        s.Cpu?.Specs.CpuName ?? "-";

    private string BuildSectionSignature(SystemSnapshot s)
    {
        int volumeCount = s.Disk?.Specs.Devices.Sum(device => device.Volumes.Count) ?? -1;
        int unattachedCount = s.Disk?.Specs.UnattachedVolumes.Count ?? -1;

        return
            $"{selectedSection}|" +
            $"{s.Cpu?.Specs.CpuName ?? "-"}|" +
            $"{s.Memory?.Specs.Devices.Count ?? -1}|" +
            $"{s.Gpu?.Specs.Devices.Count ?? -1}|" +
            $"{s.Disk?.Specs.Devices.Count ?? -1}|" +
            $"{volumeCount}|" +
            $"{unattachedCount}|" +
            $"{s.Network?.Specs.Devices.Count ?? -1}";
    }

    private void OnNavItemClicked(object? sender, MenuItemEventArgs e) =>
        e.Item?.LoadItems?.Invoke();

    private void SelectSection(Section section)
    {
        if (section == selectedSection) {
            return;
        }

        selectedSection = section;
        Draw();
    }

    internal void SelectSectionForTests(int navIndex) =>
        navMenu.MenuItems?[navIndex].LoadItems?.Invoke();

    internal ListViewItemCollection SectionItemsForTests => systemInfoView.Items;

    internal ListViewItemCollection SummaryItemsForTests => systemSummaryView.Items;

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        switch (keyInfo.Key) {
            case ConsoleKey.LeftArrow when GetFocusedControl == systemInfoView:
                navMenu.SetFocus();
                handled = true;
                Draw();
                break;

            case ConsoleKey.RightArrow when GetFocusedControl == navMenu:
                systemInfoView.SetFocus();
                handled = true;
                Draw();
                break;

            default:
                GetFocusedControl?.KeyPressed(keyInfo, ref handled);
                break;
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

        navMenu.MenuItems = new() {
            new MenuListViewItem(systemInfoView, "CPU")     { LoadItems = () => SelectSection(Section.Cpu) },
            new MenuListViewItem(systemInfoView, "MEMORY")  { LoadItems = () => SelectSection(Section.Memory) },
            new MenuListViewItem(systemInfoView, "GPU")     { LoadItems = () => SelectSection(Section.Gpu) },
            new MenuListViewItem(systemInfoView, "DISK")    { LoadItems = () => SelectSection(Section.Disk) },
            new MenuListViewItem(systemInfoView, "NETWORK") { LoadItems = () => SelectSection(Section.Network) },
        };

        systemInfoView.BackgroundColour = appConfig.Theme.Background;
        systemInfoView.ForegroundColour = appConfig.Theme.Foreground;
        systemInfoView.BorderForegroundColour = appConfig.Theme.ListViewBorderForeground;
        systemInfoView.BorderBackgroundColour = appConfig.Theme.ListViewBorderBackground;
        systemInfoView.HeaderBackgroundColour = appConfig.Theme.HeaderBackground;
        systemInfoView.HeaderForegroundColour = appConfig.Theme.HeaderForeground;
        systemInfoView.BackgroundHighlightColour = appConfig.Theme.BackgroundHighlight;
        systemInfoView.ForegroundHighlightColour = appConfig.Theme.ForegroundHighlight;
        systemInfoView.BackgroundHighlightInactiveColour = appConfig.Theme.BackgroundHighlightInactive;
        systemInfoView.ForegroundHighlightInactiveColour = appConfig.Theme.ForegroundHighlightInactive;

        logoControl.BackgroundColour = appConfig.Theme.Background;

        systemSummaryView.BackgroundColour = appConfig.Theme.Background;
        systemSummaryView.ForegroundColour = appConfig.Theme.Foreground;
        systemSummaryView.BorderForegroundColour = appConfig.Theme.ListViewBorderForeground;
        systemSummaryView.BorderBackgroundColour = appConfig.Theme.ListViewBorderBackground;

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();

        navMenu.MenuItemClicked += OnNavItemClicked;
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnResize()
    {
        navMenu.X = X;
        navMenu.Y = Y;
        navMenu.Width = NavWidth;
        navMenu.Height = Height;

        int contentX = X + NavWidth;
        int contentWidth = Width - NavWidth;
        int headerHeight = Math.Min(Height, logoControl.LogoHeight);

        logoControl.X = contentX + contentWidth - logoControl.LogoWidth - 2;
        logoControl.Y = Y;
        logoControl.Width = logoControl.LogoWidth;
        logoControl.Height = headerHeight;

        systemSummaryView.X = contentX + 1;
        systemSummaryView.Y = Y + 1;
        systemSummaryView.Width = Math.Max(1, logoControl.X - systemSummaryView.X);
        systemSummaryView.Height = Math.Max(1, headerHeight - 1);

        int summaryColWidth = Math.Max((int)(systemSummaryView.Width * 0.30), LabelColumnWidth);
        systemSummaryView.ColumnHeaders[0].Width = summaryColWidth;
        systemSummaryView.ColumnHeaders[1].Width = Math.Max(1, systemSummaryView.Width - summaryColWidth - 3);

        systemInfoView.X = contentX;
        systemInfoView.Y = Y + headerHeight;
        systemInfoView.Width = contentWidth;
        systemInfoView.Height = Height - headerHeight;

        int sizedColWidth = Math.Max((int)(Width * 0.30), LabelColumnWidth);
        systemInfoView.ColumnHeaders[0].Width = sizedColWidth;
        systemInfoView.ColumnHeaders[1].Width = Math.Max(1, systemInfoView.Width - sizedColWidth - 3);

        base.OnResize();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        navMenu.MenuItemClicked -= OnNavItemClicked;
        systemInfoView.Items.Clear();
        systemSummaryView.Items.Clear();
        lastSectionSignature = string.Empty;
        lastSummarySignature = string.Empty;

        base.OnUnload();
    }
}
