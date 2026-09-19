using System.Diagnostics;
using Task.Monitor.Gui.Commands;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Gui.Controls.DiskSpace;
using Task.Monitor.Gui.Controls.Drivers;
using Task.Monitor.Gui.Controls.InstalledApps;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Gui.Controls.Services;
using Task.Monitor.Gui.Controls.Startup;
using Task.Monitor.Gui.Controls.Summary;
using Task.Monitor.Gui.Controls.Summary2;
using Task.Monitor.Gui.Controls.SystemInformation;
using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.InputBox;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Gui;

public sealed class MainScreen2 : Screen
{
    private readonly RunContext runContext;
    private readonly ScreenApplication screenApp;

    private readonly MenuControl menuControl;
    private readonly BannerControl menuBannerControl;
    private readonly BannerControl bannerControl;
    private readonly HeaderControl2 headerControl;

    // Flip to false to revert to the original fixed-grid SummaryControl while SummaryControl2's
    // recursive split-tree layout is unproven - see the Summary2 design plan.
    private const bool UseSummaryControl2 = true;
    private readonly Control activeSummaryControl;

    private readonly PerformanceControl performanceControl;
    private readonly ProcessesControl processesControl;
    private readonly ThermalsControl thermalsControl;
    private readonly SystemInfoControl systemInfoControl;
    private readonly StartupControl startupControl;
    private readonly InstalledAppsControl installedAppsControl;
    private readonly ServicesControl servicesControl;
    private readonly DriversControl driversControl;
    private readonly DiskSpaceControl diskSpaceControl;
    private readonly FooterControl footerControl;
    private Control activeControl;
    private Control focusedControl;
    private List<Control> menuControls;
    
    private const int HeaderHeight = 3;
    private const int FooterHeight = 1;
    private const int BannerHeight = 1;
    private const int MenuWidth = 16;
    private const int ActiveControlTop = HeaderHeight + 1;

    public MainScreen2(RunContext runContext, ScreenApplication screenApp)
    : base(runContext.Terminal)
    {
        this.runContext = runContext;
        this.screenApp = screenApp;

        headerControl = new HeaderControl2(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = false
        };

        menuControl = new MenuControl(runContext.Terminal, runContext.AppConfig) {
            Visible = true,
            TabStop = true,
            TabIndex = 1
        };

        menuBannerControl = new BannerControl(runContext.Terminal, runContext.AppConfig) {
            Visible = true,
            TabStop = false,
            Text = "VIEW MENU"
        };

        bannerControl = new BannerControl(runContext.Terminal, runContext.AppConfig) {
            Visible = true,
            TabStop = false
        };

        activeSummaryControl = UseSummaryControl2
            ? new SummaryControl2(
                runContext.ServiceController,
                runContext.Terminal,
                runContext.AppConfig) {
                TabStop = true,
                TabIndex = 2
            }
            : new SummaryControl(
                runContext.ServiceController,
                runContext.Terminal,
                runContext.AppConfig) {
                TabStop = true,
                TabIndex = 2
            };

        performanceControl = new PerformanceControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = true,
            TabIndex = 2
        };

        processesControl = new ProcessesControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = true,
            TabIndex = 2
        };

        thermalsControl = new ThermalsControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = true,
            TabIndex = 2
        };

        systemInfoControl = new SystemInfoControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = true,
            TabIndex = 2
        };

        startupControl = new StartupControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = true,
            TabIndex = 2
        };

        installedAppsControl = new InstalledAppsControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = true,
            TabIndex = 2
        };

        servicesControl = new ServicesControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = true,
            TabIndex = 2
        };

        driversControl = new DriversControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = true,
            TabIndex = 2
        };

        diskSpaceControl = new DiskSpaceControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = true,
            TabIndex = 2
        };

        footerControl = new FooterControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = false,
            Visible = true
        };

        Controls
            .Add(menuControl)
            .Add(menuBannerControl)
            .Add(bannerControl)
            .Add(headerControl)
            .Add(activeSummaryControl)
            .Add(performanceControl)
            .Add(processesControl)
            .Add(thermalsControl)
            .Add(systemInfoControl)
            .Add(startupControl)
            .Add(installedAppsControl)
            .Add(servicesControl)
            .Add(driversControl)
            .Add(diskSpaceControl)
            .Add(footerControl);

        menuControls = new List<Control> {
            activeSummaryControl,
            performanceControl,
            processesControl,
            thermalsControl,
            systemInfoControl,
            startupControl,
            installedAppsControl,
            servicesControl,
            driversControl,
            diskSpaceControl
        };
        
        activeControl = activeSummaryControl;
        focusedControl = menuControl;
    } 

    internal Control? GetActiveControl => activeControl;

    internal T GetControl<T>() where T : Control => (T)Controls.Single(ctrl => ctrl is T);
    
    protected override void OnDraw()
    {
        Debug.Assert(activeControl != null);

        headerControl.Draw();
        menuControl.Draw();
        menuBannerControl.Draw();
        bannerControl.Draw();
        activeControl.Draw();
        footerControl.Draw();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        base.OnKeyPressed(keyInfo, ref handled);
        
        if (handled) {
            return;
        }
        
        switch (keyInfo.Key) {
            // Pre-existing gap, not something this session's Summary2 work introduced: the
            // constructor has always taken a ScreenApplication (screenApp), and SetupScreen/
            // HelpScreen/AboutScreen have always been registered in RunAppAction, but nothing
            // ever actually called ShowScreen<T>() for any of them - there was no way to reach
            // Setup (or Help/About) from the running app at all. This wires up F2 for Setup,
            // the screen the SummaryControl2 layout designer (LayoutDesignerScreen, reached from
            // Setup's LAYOUTS tab via 'N') needs to be reachable through. Help/About have the
            // same gap and are left alone here as out of scope for this fix.
            case ConsoleKey.F2:
                screenApp.ShowScreen<SetupScreen>();
                handled = true;
                break;

            case ConsoleKey.RightArrow when focusedControl == menuControl:
                focusedControl = activeControl;
                activeControl.SetFocus();
                Draw();
                break;

            case ConsoleKey.LeftArrow when focusedControl == activeControl:
                // The active screen gets first refusal on its own left/right navigation (e.g. a
                // nav pane and a content pane, as in SystemInfoControl). Only step back out to
                // the outer menu once it reports there is nothing further left inside it.
                activeControl.KeyPressed(keyInfo, ref handled);

                if (!handled) {
                    focusedControl = menuControl;
                    menuControl.SetFocus();
                }

                Draw();
                break;

            default:
                focusedControl.KeyPressed(keyInfo, ref handled);
                break;
        }
        
        if (handled) {
            return;
        }

        // Special case for F10 mapped to Quit - don't handle so the app loop aborts.
        if (keyInfo.Key == ConsoleKey.F10) {
            handled = false;
        }
    }

    protected override void OnLoad()
    {
        Terminal.CursorVisible = false;

        BackgroundColour = runContext.AppConfig.Theme.Background;
        ForegroundColour = runContext.AppConfig.Theme.Foreground;

        DialogBackgroundColour = runContext.AppConfig.Theme.HeaderBackground;
        DialogBorderColour = runContext.AppConfig.Theme.HeaderForeground;
        DialogForegroundColour = runContext.AppConfig.Theme.HeaderForeground;
        DialogButtonBackgroundColour = runContext.AppConfig.Theme.BackgroundHighlight;
        DialogButtonForegroundColour = runContext.AppConfig.Theme.ForegroundHighlight;
        
        foreach (Control ctrl in Controls) {
            ctrl.BackgroundColour = BackgroundColour;
            ctrl.ForegroundColour = ForegroundColour;
        }

        menuControl.MenuItems = new() {
            new MenuListViewItem(activeSummaryControl, "SUMMARY"),
            new MenuListViewItem(performanceControl, "PERFORMANCE"),
            new MenuListViewItem(processesControl,   "PROCESSES"),
            new MenuListViewItem(thermalsControl,    "THERMALS"),
            new MenuListViewItem(diskSpaceControl,   "DISK SPACE"),
            new MenuListViewItem(startupControl,     "STARTUP"),
            new MenuListViewItem(installedAppsControl, "APPS"),
            new MenuListViewItem(servicesControl,    "SERVICES"),
            new MenuListViewItem(driversControl,     "DRIVERS"),
            new MenuListViewItem(systemInfoControl,  "SYSTEM INFO"),
        };
        
        activeControl = activeSummaryControl;
        bannerControl.Text = menuControl.MenuItems[0].Text;
        focusedControl = menuControl;

        headerControl.Load();
        menuControl.Load();
        menuBannerControl.Load();
        bannerControl.Load();
        activeControl.Load();
        footerControl.Load();
        
        menuControl.SetFocus();
        menuControl.MenuItemClicked += OnMenuItemClicked;
    }

    private void OnMenuItemClicked(object? sender, MenuItemEventArgs e) =>
        SetActiveControl(e.Item!);        

    protected override void OnResize()
    {
        int headerHeight = HeaderHeight;
        
        headerControl.X = 0;
        headerControl.Y = 0;
        headerControl.Height = headerHeight;
        headerControl.Width = Width;
        headerControl.Resize();

        menuControl.X = 0;
        menuControl.Y = ActiveControlTop + 1;
        menuControl.Width = MenuWidth;
        menuControl.Height = Height - ActiveControlTop - FooterHeight - 1;
        menuControl.Resize();

        menuBannerControl.X = 0;
        menuBannerControl.Y = ActiveControlTop;
        menuBannerControl.Width = MenuWidth;
        menuBannerControl.Height = BannerHeight;
        menuBannerControl.Resize();

        bannerControl.X = MenuWidth + 1;
        bannerControl.Y = ActiveControlTop;
        bannerControl.Width = Width - (MenuWidth + 1);
        bannerControl.Height = BannerHeight;
        bannerControl.Resize();

        SizeControl(activeControl);
        
        footerControl.X = 0;
        footerControl.Y = Height - FooterHeight;
        // Setting Width -1 prevents the auto-scroll when bottom right corner is written to on Windows, 
        // which can sometimes jump the screen around.
        footerControl.Width = Width - 1;
        footerControl.Height = FooterHeight;
        footerControl.Resize();
    }
    
    protected override void OnUnload()
    {
        base.OnUnload();
        menuControl.MenuItemClicked -= OnMenuItemClicked;
        Terminal.CursorVisible = true;
    }

    internal void SetActiveControl(MenuListViewItem item)
    {
        activeControl.Unload();
        activeControl = item.AssociatedControl;
        
        bannerControl.Text = item.Text;
        bannerControl.Draw();

        activeControl.Load();
        SizeControl(activeControl);
        activeControl.Clear();
        activeControl.Draw();

        // Loading the newly-previewed pane can establish its own default internal focus (e.g.
        // ProcessControl/SystemInfoControl focusing their own nav on load) - this is only ever
        // reached while the outer menu is what the user is actually navigating, so real focus
        // belongs back on the menu, not silently left on whatever the pane just claimed.
        menuControl.SetFocus();
    }

    private void SizeControl(Control control)
    {
        control.X = menuControl.X + menuControl.Width;
        control.Y = ActiveControlTop + BannerHeight;
        control.Width = Width - menuControl.Width;
        control.Height = Height - BannerHeight - ActiveControlTop - FooterHeight;
        control.Resize();
    }
}
