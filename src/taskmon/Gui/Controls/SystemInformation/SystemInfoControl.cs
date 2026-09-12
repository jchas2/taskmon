using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.SystemInformation;

// A vertical nav (SYSTEM/CPU/MEMORY/GPU/DISK/NETWORK) on the left drives which single section is
// shown, full width, in the list view on the right - one property-name/value list per section
// rather than every section scrolled together. Rows within a section are grouped by device (a
// SLOT/GPU/DISK sub-header per device) where the subsystem has more than one.
public sealed partial class SystemInfoControl : Control
{
    private enum Section { System, Cpu, Memory, Gpu, Disk, Network }

    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly MenuControl navMenu;
    private readonly ListView systemInfoView;

    // The most recent snapshot, kept whole. The rows are rebuilt from it only when the set of
    // devices changes or the selected section changes (see BuildSignature); the values themselves
    // are specification facts that do not move tick to tick.
    private SystemSnapshot? snapshot;
    private string lastSignature = string.Empty;
    private Section selectedSection = Section.System;

    private const int LabelColumnWidth = 32;
    private const int NavWidth = 12;
    private const int ControlGutter = 1;
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
            HeaderText = "SYSTEM INFORMATION"
        };

        // Hidden, but the two column widths still drive the row layout the same way they do on
        // the About screen.
        systemInfoView.ColumnHeaders
            .Add(new ListViewColumnHeader(string.Empty))
            .Add(new ListViewColumnHeader(string.Empty));

        Controls
            .Add(navMenu)
            .Add(systemInfoView);
    }

    // Called with the latest snapshot every publish. Wired to the controller event in OnLoad;
    // tests call it directly.
    public void Sample(SystemSnapshot snapshot)
    {
        this.snapshot = snapshot;
        Draw();
    }

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();

            // A null snapshot still lays out the selected section; the per-service rows fill in
            // as each service publishes.
            EnsureRows(snapshot ?? new SystemSnapshot());

            navMenu.Draw();
            systemInfoView.Draw();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    // Rebuilds the row list only when the shape changes: the first few ticks as the services come
    // online, a section switch, then not again until a device is added or removed. A rebuild resets
    // the scroll to the top, which is why it is gated rather than run every draw.
    private void EnsureRows(SystemSnapshot snapshot)
    {
        string signature = BuildSignature(snapshot);

        if (signature == lastSignature) {
            return;
        }

        lastSignature = signature;
        RebuildRows(snapshot);
    }

    private string BuildSignature(SystemSnapshot s)
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

    // Test-only seam: invokes the same LoadItems action a real nav selection (click or arrow-key
    // move onto the row) would fire. Avoids needing a parent Screen just to make SetFocus() and
    // keyboard routing exercise the nav in a unit test.
    internal void SelectSectionForTests(int navIndex) =>
        navMenu.MenuItems?[navIndex].LoadItems?.Invoke();

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        try {
            Control.DrawingLockAcquire();

            switch (keyInfo.Key) {
                // Only claimed when there is somewhere internal left/right to move: on the
                // content pane, left steps back to the nav; on the nav, right steps into the
                // content. Otherwise the key is left unhandled so MainScreen2 can move focus back
                // to its own outer menu (left) or leaves right to do nothing further (there is no
                // pane beyond the content).
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
        finally {
            Control.DrawingLockRelease();
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        // MenuControl.OnLoad throws if MenuItems is still null, so this has to be set before the
        // base.OnLoad() below reaches it.
        navMenu.MenuItems = new() {
            new MenuListViewItem(systemInfoView, "SYSTEM")  { LoadItems = () => SelectSection(Section.System) },
            new MenuListViewItem(systemInfoView, "CPU")     { LoadItems = () => SelectSection(Section.Cpu) },
            new MenuListViewItem(systemInfoView, "MEMORY")  { LoadItems = () => SelectSection(Section.Memory) },
            new MenuListViewItem(systemInfoView, "GPU")     { LoadItems = () => SelectSection(Section.Gpu) },
            new MenuListViewItem(systemInfoView, "DISK")    { LoadItems = () => SelectSection(Section.Disk) },
            new MenuListViewItem(systemInfoView, "NETWORK") { LoadItems = () => SelectSection(Section.Network) },
        };

        systemInfoView.BackgroundColour = appConfig.DefaultTheme.Background;
        systemInfoView.ForegroundColour = appConfig.DefaultTheme.Foreground;
        systemInfoView.BorderColour = appConfig.DefaultTheme.ListViewBorder;
        systemInfoView.HeaderBackgroundColour = appConfig.DefaultTheme.HeaderBackground;
        systemInfoView.HeaderForegroundColour = appConfig.DefaultTheme.HeaderForeground;
        systemInfoView.BackgroundHighlightColour = appConfig.DefaultTheme.BackgroundHighlight;
        systemInfoView.ForegroundHighlightColour = appConfig.DefaultTheme.ForegroundHighlight;

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();

        navMenu.MenuItemClicked += OnNavItemClicked;
        navMenu.SetFocus();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnResize()
    {
        navMenu.X = X;
        navMenu.Y = Y;
        navMenu.Width = NavWidth;
        navMenu.Height = Height;

        systemInfoView.X = X + NavWidth + ControlGutter;
        systemInfoView.Y = Y;
        systemInfoView.Width = Width - NavWidth - ControlGutter;
        systemInfoView.Height = Height;

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
        lastSignature = string.Empty;

        base.OnUnload();
    }
}
