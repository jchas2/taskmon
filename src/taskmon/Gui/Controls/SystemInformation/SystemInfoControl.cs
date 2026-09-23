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
    private enum Section { Cpu, Memory, Gpu, Disk, Network }

    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly MenuControl navMenu;
    private readonly ListView systemInfoView;
    private readonly SystemLogoControl logoControl;
    private readonly ListView systemSummaryView;

    // The most recent snapshot, kept whole. The rows are rebuilt from it only when the set of
    // devices changes or the selected section changes (see BuildSectionSignature); the values
    // themselves are specification facts that do not move tick to tick.
    private SystemSnapshot? snapshot;

    // Tracked separately from the section signature: the pinned summary header is never rebuilt
    // just because the nav selection changed, only when its own content actually could have.
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

        // Hidden, but the two column widths still drive the row layout the same way they do on
        // the About screen.
        systemInfoView.ColumnHeaders
            .Add(new ListViewColumnHeader(string.Empty))
            .Add(new ListViewColumnHeader(string.Empty));

        logoControl = new SystemLogoControl(terminal) {
            Visible = true
        };

        // Pinned above systemInfoView, always showing the machine/OS/CPU/memory/GPU/disk summary
        // regardless of which nav section is selected below it - never focusable, never scrolled.
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

    // SystemInfoControl itself draws no border - navMenu is the actual bordered, focusable panel
    // that owns internal left/right routing to systemInfoView - so a SetFocus() call on this
    // composite (e.g. from MainScreen2's arrow-key nav) needs to be redirected down to it for the
    // focus-colour cue to reach anything visible.
    protected override void OnGotFocus() => navMenu.SetFocus();

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    // Called with the latest snapshot every publish. Wired to the controller event in OnLoad;
    // tests call it directly.
    public void Sample(SystemSnapshot snapshot)
    {
        this.snapshot = snapshot;
        Draw();
    }

    protected override void OnDraw()
    {
        // A null snapshot still lays out the selected section; the per-service rows fill in
        // as each service publishes.
        SystemSnapshot s = snapshot ?? new SystemSnapshot();

        EnsureSummaryRows(s);
        EnsureSectionRows(s);

        logoControl.Draw();
        systemSummaryView.Draw();
        navMenu.Draw();
        systemInfoView.Draw();
    }

    // Rebuilds the pinned summary header only when its own content could have changed - never on
    // a nav selection change, since it is always shown regardless of which section is selected.
    private void EnsureSummaryRows(SystemSnapshot snapshot)
    {
        string signature = BuildSummarySignature(snapshot);

        if (signature == lastSummarySignature) {
            return;
        }

        lastSummarySignature = signature;
        RebuildSummaryRows(snapshot);
    }

    // Rebuilds the row list only when the shape changes: the first few ticks as the services come
    // online, a section switch, then not again until a device is added or removed. A rebuild resets
    // the scroll to the top, which is why it is gated rather than run every draw.
    private void EnsureSectionRows(SystemSnapshot snapshot)
    {
        string signature = BuildSectionSignature(snapshot);

        if (signature == lastSectionSignature) {
            return;
        }

        lastSectionSignature = signature;
        RebuildRows(snapshot);
    }

    // Keyed only on what AddSystemSection actually reads that can change row count: CPU identity.
    // Memory is shown as a single total (no per-device rows), and GPU/disk/network are not shown
    // at all, so none of those belong in this signature.
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

    // Test-only seam: invokes the same LoadItems action a real nav selection (click or arrow-key
    // move onto the row) would fire. Avoids needing a parent Screen just to make SetFocus() and
    // keyboard routing exercise the nav in a unit test.
    internal void SelectSectionForTests(int navIndex) =>
        navMenu.MenuItems?[navIndex].LoadItems?.Invoke();

    // Test-only seams: the rows as last built, so tests can inspect per-cell colours that the
    // captured terminal output does not carry in a readable form.
    internal ListViewItemCollection SectionItemsForTests => systemInfoView.Items;

    internal ListViewItemCollection SummaryItemsForTests => systemSummaryView.Items;

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
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

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

        // MenuControl.OnLoad throws if MenuItems is still null, so this has to be set before the
        // base.OnLoad() below reaches it.
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
        // Unchanged in position/size, per design: the pinned header above the detail pane never
        // affects the nav column.
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

        // Sized from the actual gaps to its neighbours (logoControl.X, headerHeight) rather than
        // independently, so the +1 indent below never overlaps the logo horizontally or
        // systemInfoView vertically.
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
