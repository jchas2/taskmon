using System.Diagnostics;
using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Gui.Controls.DiskSpace;
using Task.Monitor.Gui.Controls.Drivers;
using Task.Monitor.Gui.Controls.Health;
using Task.Monitor.Gui.Controls.InstalledApps;
using Task.Monitor.Gui.Controls.LaunchdServices;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Gui.Controls.Services;
using Task.Monitor.Gui.Controls.Startup;
using Task.Monitor.Gui.Controls.Summary2;
using Task.Monitor.Gui.Controls.SystemInformation;
using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.InputBox;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Gui;

public sealed class MainScreen2 : Screen
{
    private readonly RunContext        runContext;
    private readonly ScreenApplication screenApp;

    private readonly AnsiScreenBuffer     frame = new();
    private readonly ListView             menuView;
    private readonly HeaderControl2       headerControl;
    private readonly SummaryControl2      summaryControl;
    private readonly HealthControl        healthControl;
    private readonly PerformanceControl   performanceControl;
    private readonly ProcessesControl     processesControl;
    private readonly ThermalsControl      thermalsControl;
    private readonly SystemInfoControl    systemInfoControl;
    private readonly StartupControl       startupControl;
    private readonly InstalledAppsControl installedAppsControl;
    private readonly Control              servicesControl;
    private readonly DriversControl       driversControl;
    private readonly DiskSpaceControl     diskSpaceControl;
    private readonly FooterControl        footerControl;
    
    private Control activeControl;
    private Control focusedControl;
    private string  bannerText = string.Empty;

    private const int HeaderHeight     = HeaderControl2.HeaderRows;
    private const int BannerHeight     = 1;
    private const int MenuWidth        = 16;
    private const int ActiveControlTop = HeaderHeight;

    public MainScreen2(RunContext runContext, ScreenApplication screenApp)
    : base(runContext.Terminal)
    {
        this.runContext = runContext;
        this.screenApp  = screenApp;

        headerControl = new HeaderControl2(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop = false
        };

        menuView = new ListView(runContext.Terminal) {
            ShowColumnHeaders = false,
            TabStop           = true,
            TabIndex          = 1
        };

        menuView.ColumnHeaders.Add(new ListViewColumnHeader(string.Empty));
        menuView.ItemClicked += OnMenuItemClicked;

        summaryControl = new SummaryControl2(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop  = true,
            TabIndex = 2
        };

        healthControl = new HealthControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop  = true,
            TabIndex = 2
        };

        performanceControl = new PerformanceControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop  = true,
            TabIndex = 2
        };

        processesControl = new ProcessesControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop  = true,
            TabIndex = 2
        };

        thermalsControl = new ThermalsControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop  = true,
            TabIndex = 2
        };

        systemInfoControl = new SystemInfoControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop  = true,
            TabIndex = 2
        };

        startupControl = new StartupControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop  = true,
            TabIndex = 2
        };

        installedAppsControl = new InstalledAppsControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop  = true,
            TabIndex = 2
        };

#if __APPLE__
        servicesControl = new LaunchdServicesControl(
#else
        servicesControl = new ServicesControl(
#endif
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop  = true,
            TabIndex = 2
        };

        driversControl = new DriversControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop  = true,
            TabIndex = 2
        };

        diskSpaceControl = new DiskSpaceControl(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            TabStop  = true,
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
            .Add(menuView)
            .Add(headerControl)
            .Add(summaryControl)
            .Add(healthControl)
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

        activeControl  = summaryControl;
        focusedControl = menuView;
    }

    private void ApplyThemeDefaults()
    {
        CommonControl.ThemeDefaults(this, runContext.AppConfig.Theme);

        foreach (Control ctrl in Controls) {
            CommonControl.ThemeDefaults(ctrl, runContext.AppConfig.Theme);
        }

        CommonControl.Theme(menuView, runContext.AppConfig.Theme);
    }

    private void DrawBanner(int x, int width, string text)
    {
        if (width < 2) {
            return;
        }

        Color background = runContext.AppConfig.Theme.MenubarBackground;

        frame.Clear();
        frame.MoveTo(x, ActiveControlTop);
        AppendHalfBlock(frame, BackgroundColour, background, fallback: BackgroundColour);
        frame.SetColour(runContext.AppConfig.Theme.MenubarForeground, background);
        frame.Append(text.PadOrTruncate(' ', width - 2));
        AppendHalfBlock(frame, background, BackgroundColour, fallback: BackgroundColour);
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

    private int FooterHeight => footerControl.Rows;

    internal Control? GetActiveControl => activeControl;

    internal T GetControl<T>() where T : Control => (T)Controls.Single(ctrl => ctrl is T);

    internal ListView MenuView => menuView;

    protected override void OnDraw()
    {
        Debug.Assert(activeControl != null);

        headerControl.Draw();
        menuView.Draw();
        DrawBanner(0, MenuWidth, Strings.MainMenuTitle);
        DrawBanner(MenuWidth, Width - MenuWidth, bannerText);
        activeControl.Draw();
        footerControl.Draw();
    }

    private void OnHealthProcessRequested(object? sender, int pid)
    {
        SetActiveControl(SelectMenuItem(processesControl));
        focusedControl = processesControl;
        processesControl.SetFocus();
        processesControl.ProcessControl.SelectProcess(pid);
        Draw();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (!Control.RedrawEnabled) {
            focusedControl.KeyPressed(keyInfo, ref handled);
            return;
        }

        base.OnKeyPressed(keyInfo, ref handled);
        
        if (handled) {
            return;
        }
        
        switch (keyInfo.Key) {
            case ConsoleKey.F1:
                screenApp.ShowScreen<HelpScreen>();
                handled = true;
                break;

            case ConsoleKey.F2:
                screenApp.ShowScreen<SetupScreen>();
                handled = true;
                break;

            case ConsoleKey.F3:
                ShowNextTheme();
                handled = true;
                break;

            case ConsoleKey.F4:
                ShowNextLayout();
                handled = true;
                break;

            case ConsoleKey.F12:
                screenApp.ShowScreen<AboutScreen>();
                handled = true;
                break;

            case ConsoleKey.RightArrow when focusedControl == menuView:
                if (activeControl is SummaryControl2 { HasFocusablePane: false }) {
                    break;
                }

                focusedControl = activeControl;
                activeControl.SetFocus();
                Draw();
                break;

            case ConsoleKey.LeftArrow when focusedControl == activeControl:
                activeControl.KeyPressed(keyInfo, ref handled);

                if (!handled) {
                    focusedControl = menuView;
                    menuView.SetFocus();
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

        if (keyInfo.Key == ConsoleKey.T && runContext.AppConfig.ShowTips) {
            footerControl.NextTip();
            handled = true;
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

        ApplyThemeDefaults();

        menuView.Items.Clear();

        menuView.Items.AddRange(
            new MenuListViewItem(summaryControl,       Strings.MainMenuSummary),
            new MenuListViewItem(healthControl,        Strings.MainMenuHealth),
            new MenuListViewItem(performanceControl,   Strings.MainMenuPerformance),
            new MenuListViewItem(processesControl,     Strings.MainMenuProcesses),
            new MenuListViewItem(thermalsControl,      Strings.MainMenuThermals),
            new MenuListViewItem(diskSpaceControl,     Strings.MainMenuDiskSpace),
            new MenuListViewItem(startupControl,       Strings.MainMenuStartup),
            new MenuListViewItem(installedAppsControl, Strings.MainMenuApps),
            new MenuListViewItem(servicesControl,      Strings.MainMenuServices),
            new MenuListViewItem(driversControl,       Strings.MainMenuDrivers),
            new MenuListViewItem(systemInfoControl,    Strings.MainMenuSystemInfo));

        activeControl  = summaryControl;
        bannerText     = Strings.MainMenuSummary;
        focusedControl = menuView;

        foreach (Control control in ShownControls) {
            control.Load();
        }

        menuView.SetFocus();
        healthControl.ProcessRequested += OnHealthProcessRequested;
    }

    private void OnMenuItemClicked(object? sender, ListViewItemEventArgs e)
    {
        if (e.Item is MenuListViewItem item && item.AssociatedControl != activeControl) {
            SetActiveControl(item);
        }
    }

    protected override void OnResize()
    {
        int headerHeight = HeaderHeight;
        
        headerControl.X      = 0;
        headerControl.Y      = 0;
        headerControl.Height = headerHeight;
        headerControl.Width = Width;
        headerControl.Resize();

        menuView.X      = 0;
        menuView.Y      = ActiveControlTop + BannerHeight;
        menuView.Width  = MenuWidth;
        menuView.Height = Height - ActiveControlTop - FooterHeight - BannerHeight;
        menuView.ColumnHeaders[0].Width = MenuWidth - 2;
        menuView.Resize();

        SizeControl(activeControl);
        
        footerControl.X      = 0;
        footerControl.Y      = Height - FooterHeight;
        // Setting Width -1 prevents the auto-scroll when bottom right corner is written to on Windows, 
        // which can sometimes jump the screen around.
        footerControl.Width  = Width - 1;
        footerControl.Height = FooterHeight;
        footerControl.Resize();
    }
    
    protected override void OnUnload()
    {
        base.OnUnload();
        healthControl.ProcessRequested -= OnHealthProcessRequested;
        Terminal.CursorVisible = true;
    }

    internal MenuListViewItem SelectMenuItem(Control control)
    {
        MenuListViewItem item = menuView.Items
            .OfType<MenuListViewItem>()
            .First(candidate => candidate.AssociatedControl == control);
        
        menuView.SelectedIndex = menuView.Items.IndexOf(item);

        return item;
    }

    internal void SetActiveControl(MenuListViewItem item)
    {
        activeControl.Unload();
        activeControl = item.AssociatedControl;

        bannerText = item.Text;
        DrawBanner(MenuWidth, Width - MenuWidth, bannerText);

        activeControl.Load();
        SizeControl(activeControl);
        activeControl.Clear();
        activeControl.Draw();

        menuView.SetFocus();
    }

    private IEnumerable<Control> ShownControls =>
        [headerControl, menuView, activeControl, footerControl];

    private void ShowNextLayout()
    {
        List<SummaryControlLayout> layouts = runContext.AppConfig.Layouts.OrderBy(l => l.Name).ToList();

        if (layouts.Count < 2) {
            return;
        }

        int index = layouts.FindIndex(l => l == runContext.AppConfig.DefaultLayout);
        runContext.AppConfig.DefaultLayout = layouts[(index + 1) % layouts.Count];

        bool viewHadFocus = focusedControl == activeControl;

        if (activeControl == summaryControl) {
            summaryControl.Unload();
            summaryControl.Load();
        }
        else {
            SetActiveControl(SelectMenuItem(summaryControl));
        }

        if (viewHadFocus && summaryControl.HasFocusablePane) {
            focusedControl = summaryControl;
            summaryControl.SetFocus();
        }
        else {
            focusedControl = menuView;
            menuView.SetFocus();
        }

        Clear();
        Resize();
        Draw();
    }

    private void ShowNextTheme()
    {
        List<Theme> themes = runContext.AppConfig.Themes.OrderBy(t => t.Name).ToList();

        if (themes.Count < 2) {
            return;
        }

        runContext.AppConfig.Theme = themes[(themes.IndexOf(runContext.AppConfig.Theme) + 1) % themes.Count];

        Control? focus = CurrentFocus;

        ApplyThemeDefaults();

        foreach (Control control in ShownControls) {
            control.Unload();
            control.Load();
        }

        focus?.SetFocus();
        SelectMenuItem(activeControl);

        Clear();
        Resize();
        Draw();
    }

    private void SizeControl(Control control)
    {
        control.X      = MenuWidth;
        control.Y      = ActiveControlTop + BannerHeight;
        control.Width  = Width - MenuWidth;
        control.Height = Height - BannerHeight - ActiveControlTop - FooterHeight;
        control.Resize();
    }
}
