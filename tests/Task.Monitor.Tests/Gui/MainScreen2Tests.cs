using System.Reflection;
using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Gui.Controls.Health;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Gui.Controls.Summary2;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Services.Process;
using Task.Monitor.Tests.Common;
using ServiceController = Task.Monitor.System.Services.ServiceController;
using SystemSnapshot = Task.Monitor.System.Services.SystemSnapshot;
using SystemSnapshotEventArgs = Task.Monitor.System.Services.SystemSnapshotEventArgs;

namespace Task.Monitor.Tests.Gui;

public sealed class MainScreen2Tests : IDisposable
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;
    private readonly ScreenApplication screenApp;

    // The top of a SUMMARY chart's scale, drawn only when SUMMARY itself is drawn.
    private const string SummaryChartScale = "100%┤";

    public MainScreen2Tests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
        screenApp = new ScreenApplication(runContext.Terminal);
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    public void Dispose() => Control.RedrawEnabled = true;

    [Fact]
    public void Enter_On_A_Health_Cause_Opens_That_Process_In_Processes()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        HealthControl    healthControl    = mainScreen.GetControl<HealthControl>();
        ProcessesControl processesControl = mainScreen.GetControl<ProcessesControl>();
        bool             handled          = false;

        ProcessInfo processes = new() {
            Metrics = new ProcessMetrics {
                Entries = [
                    new ProcessEntry { Pid = 1111, ProcessName = "first",  CpuTimePercent = 0.9 },
                    new ProcessEntry { Pid = 2222, ProcessName = "second", CpuTimePercent = 0.1 }
                ]
            }
        };

        mainScreen.SetActiveControl(new MenuListViewItem(healthControl, "HEALTH"));
        mainScreen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);

        RaiseSnapshotUpdated(new SystemSnapshot {
            Processes = processes,
            Health    = new HealthInfo {
                Level     = HealthLevel.Warning,
                Resources = [new HealthReading { Resource = HealthResource.Cpu, Level = HealthLevel.Warning }],
                Culprits  = [new HealthCulprit("second", 1, 2222, "10% CPU", [HealthResource.Cpu])]
            }
        });

        mainScreen.KeyPressed(Key(ConsoleKey.Enter), ref handled);

        Assert.Same(processesControl, mainScreen.GetActiveControl);

        RaiseSnapshotUpdated(new SystemSnapshot { Processes = processes });

        Assert.Equal(2222, processesControl.ProcessControl.SelectedProcessId);
    }

    [Fact]
    public void F10_Is_Left_Unhandled_So_The_App_Exits()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();

        bool handled = false;
        mainScreen.KeyPressed(Key(ConsoleKey.F10), ref handled);

        Assert.False(handled);
    }

    [Fact]
    public void F1_Shows_The_Help_Screen()
    {
        MainScreen2 mainScreen = new(runContext, screenApp) {
            Width = 120,
            Height = 40
        };

        screenApp
            .RegisterScreen(mainScreen)
            .RegisterScreen(new HelpScreen(runContext));

        screenApp.ShowScreen<MainScreen2>();
        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        mainScreen.KeyPressed(Key(ConsoleKey.F1), ref handled);

        Assert.True(handled);
        Assert.Contains("TASK MONITOR HELP", CapturedOutput());
    }

    [Fact]
    public void F2_And_Tab_Reach_A_Process_Filter_Dialog_Instead_Of_The_Screen()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        ProcessesControl processesControl = mainScreen.GetControl<ProcessesControl>();
        ProcessControl   processControl   = processesControl.ProcessControl;
        bool             handled          = false;

        mainScreen.SetActiveControl(new MenuListViewItem(processesControl, "PROCESSES"));
        mainScreen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);
        mainScreen.KeyPressed(new ConsoleKeyInfo('f', ConsoleKey.F, false, false, false), ref handled);

        Assert.True(processControl.IsFilterDialogVisible);

        runContextHelper.terminal.Invocations.Clear();
        mainScreen.KeyPressed(Key(ConsoleKey.F2), ref handled);

        Assert.DoesNotContain("TASK MONITOR SETUP", CapturedOutput());
        Assert.True(processControl.IsFilterDialogVisible);

        mainScreen.KeyPressed(new ConsoleKeyInfo('x', 0, false, false, false), ref handled);
        mainScreen.KeyPressed(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false), ref handled);
        mainScreen.KeyPressed(Key(ConsoleKey.Enter), ref handled);

        Assert.False(processControl.IsFilterDialogVisible);
        Assert.Empty(processControl.FilterText);
    }

    // Regression test: MainScreen2's constructor has always taken a ScreenApplication, and
    // SetupScreen/HelpScreen/AboutScreen have always been registered in RunAppAction, but nothing
    // ever actually called ShowScreen<T>() for any of them - Setup was unreachable from the
    // running app. F2 is the fix, needed so LayoutDesignerScreen (reached from Setup's LAYOUTS
    // tab) is reachable at all.
    [Fact]
    public void F2_Shows_The_Setup_Screen()
    {
        MainScreen2 mainScreen = new(runContext, screenApp) {
            Width = 120,
            Height = 40
        };

        SetupScreen setupScreen = new(runContext, screenApp);

        screenApp
            .RegisterScreen(mainScreen)
            .RegisterScreen(setupScreen);

        screenApp.ShowScreen<MainScreen2>();
        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        mainScreen.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.F2, false, false, false), ref handled);

        Assert.True(handled);
        Assert.Contains("TASK MONITOR SETUP", CapturedOutput());
    }

    [Fact]
    public void F3_Does_Nothing_While_A_Dialog_Is_Open()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        ProcessesControl processesControl = mainScreen.GetControl<ProcessesControl>();
        Theme            theme            = runContext.AppConfig.Theme;
        bool             handled          = false;

        mainScreen.SetActiveControl(new MenuListViewItem(processesControl, "PROCESSES"));
        mainScreen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);
        mainScreen.KeyPressed(new ConsoleKeyInfo('f', ConsoleKey.F, false, false, false), ref handled);
        Assert.True(processesControl.ProcessControl.IsFilterDialogVisible);

        mainScreen.KeyPressed(Key(ConsoleKey.F3), ref handled);

        Assert.Same(theme, runContext.AppConfig.Theme);
        Assert.True(processesControl.ProcessControl.IsFilterDialogVisible);
    }

    // PROCESSES moves focus to its details pane when it loads, which F3's reload must not keep.
    [Fact]
    public void F3_Keeps_Focus_On_The_Menu()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        ProcessesControl processesControl = mainScreen.GetControl<ProcessesControl>();
        MenuControl      menuControl      = mainScreen.GetControl<MenuControl>();
        bool             handled          = false;

        mainScreen.SetActiveControl(menuControl.Select(processesControl));
        Assert.True(menuControl.Focused);

        mainScreen.KeyPressed(Key(ConsoleKey.F3), ref handled);

        Assert.True(menuControl.Focused);
        Assert.False(processesControl.HasFocus);
    }

    [Fact]
    public void F3_Keeps_The_Current_View_Its_Menu_Item_And_Focus()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        ProcessesControl processesControl = mainScreen.GetControl<ProcessesControl>();
        MenuControl      menuControl      = mainScreen.GetControl<MenuControl>();
        bool             handled          = false;

        mainScreen.SetActiveControl(menuControl.Select(processesControl));
        mainScreen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);
        Assert.True(processesControl.ProcessControl.HasFocus);

        handled = false;
        mainScreen.KeyPressed(Key(ConsoleKey.F3), ref handled);

        Assert.True(handled);
        Assert.Same(processesControl, mainScreen.GetActiveControl);
        Assert.Same(processesControl, menuControl.SelectedItem?.AssociatedControl);
        Assert.True(processesControl.ProcessControl.HasFocus);
        Assert.False(menuControl.Focused);
    }

    [Fact]
    public void F3_Recolours_The_Controls_On_Screen()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        ProcessesControl processesControl = mainScreen.GetControl<ProcessesControl>();
        List<Theme>      themes           = ThemesInSetupOrder();
        bool             handled          = false;

        // Two neighbouring themes with different backgrounds, so the change can be seen.
        int first = Enumerable.Range(0, themes.Count - 1)
            .First(i => themes[i].Background.ToArgb() != themes[i + 1].Background.ToArgb());

        runContext.AppConfig.Theme = themes[first];
        mainScreen.SetActiveControl(new MenuListViewItem(processesControl, "PROCESSES"));

        mainScreen.KeyPressed(Key(ConsoleKey.F3), ref handled);

        // The process list takes its colours from its parent's load, not from the screen directly.
        Assert.Equal(themes[first + 1].Background.ToArgb(), processesControl.ProcessControl.BackgroundColour.ToArgb());
    }

    [Fact]
    public void F3_Switches_To_The_Next_Theme_In_Setup_Order_And_Wraps()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        List<Theme> themes  = ThemesInSetupOrder();
        bool        handled = false;

        runContext.AppConfig.Theme = themes[0];
        mainScreen.KeyPressed(Key(ConsoleKey.F3), ref handled);
        Assert.Same(themes[1], runContext.AppConfig.Theme);

        runContext.AppConfig.Theme = themes[^1];
        mainScreen.KeyPressed(Key(ConsoleKey.F3), ref handled);
        Assert.Same(themes[0], runContext.AppConfig.Theme);
    }

    [Fact]
    public void F4_Does_Nothing_While_A_Dialog_Is_Open()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        ProcessesControl      processesControl = mainScreen.GetControl<ProcessesControl>();
        SummaryControlLayout? layout           = runContext.AppConfig.DefaultLayout;
        bool                  handled          = false;

        mainScreen.SetActiveControl(new MenuListViewItem(processesControl, "PROCESSES"));
        mainScreen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);
        mainScreen.KeyPressed(new ConsoleKeyInfo('f', ConsoleKey.F, false, false, false), ref handled);
        Assert.True(processesControl.ProcessControl.IsFilterDialogVisible);

        mainScreen.KeyPressed(Key(ConsoleKey.F4), ref handled);

        Assert.Same(layout, runContext.AppConfig.DefaultLayout);
        Assert.Same(processesControl, mainScreen.GetActiveControl);
        Assert.True(processesControl.ProcessControl.IsFilterDialogVisible);
    }

    [Fact]
    public void F4_Keeps_Focus_On_The_Menu()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        MenuControl menuControl = mainScreen.GetControl<MenuControl>();
        bool        handled     = false;

        Assert.True(menuControl.Focused);

        mainScreen.KeyPressed(Key(ConsoleKey.F4), ref handled);

        Assert.Same(mainScreen.GetControl<SummaryControl2>(), mainScreen.GetActiveControl);
        Assert.True(menuControl.Focused);
    }

    [Fact]
    public void F4_Moves_Focus_From_A_View_To_The_First_Summary_Pane()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        ProcessesControl processesControl = mainScreen.GetControl<ProcessesControl>();
        SummaryControl2  summaryControl   = mainScreen.GetControl<SummaryControl2>();
        MenuControl      menuControl      = mainScreen.GetControl<MenuControl>();
        bool             handled          = false;

        mainScreen.SetActiveControl(menuControl.Select(processesControl));
        mainScreen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);
        Assert.True(processesControl.ProcessControl.HasFocus);

        mainScreen.KeyPressed(Key(ConsoleKey.F4), ref handled);

        Assert.True(summaryControl.PaneControls.Values.First().HasFocus);
        Assert.False(menuControl.Focused);

        // Keys now reach SUMMARY: Left from its first pane goes back to the menu.
        handled = false;
        mainScreen.KeyPressed(Key(ConsoleKey.LeftArrow), ref handled);
        Assert.True(menuControl.Focused);
    }

    [Fact]
    public void F4_On_Another_View_Opens_Summary()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        SummaryControl2 summaryControl = mainScreen.GetControl<SummaryControl2>();
        MenuControl     menuControl    = mainScreen.GetControl<MenuControl>();
        bool            handled        = false;

        mainScreen.SetActiveControl(menuControl.Select(mainScreen.GetControl<ProcessesControl>()));
        mainScreen.KeyPressed(Key(ConsoleKey.F4), ref handled);

        Assert.Same(summaryControl, mainScreen.GetActiveControl);
        Assert.Same(summaryControl, menuControl.SelectedItem?.AssociatedControl);
        Assert.True(menuControl.Focused);
    }

    [Fact]
    public void F4_Rebuilds_The_Summary_Panes_For_The_New_Layout()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        SummaryControl2            summaryControl = mainScreen.GetControl<SummaryControl2>();
        List<SummaryControlLayout> layouts        = LayoutsInSetupOrder();
        List<Control>              before         = summaryControl.PaneControls.Values.ToList();
        bool                       handled        = false;

        SummaryControlLayout next = layouts[(layouts.FindIndex(l => l == runContext.AppConfig.DefaultLayout) + 1) % layouts.Count];

        mainScreen.KeyPressed(Key(ConsoleKey.F4), ref handled);

        Assert.Equal(next.ToTree().Panes().Count(), summaryControl.PaneControls.Count);
        Assert.DoesNotContain(summaryControl.PaneControls.Values, before.Contains);
    }

    [Fact]
    public void F4_Switches_To_The_Next_Layout_In_Setup_Order_And_Wraps()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        List<SummaryControlLayout> layouts = LayoutsInSetupOrder();
        bool                       handled = false;

        runContext.AppConfig.DefaultLayout = layouts[0];
        mainScreen.KeyPressed(Key(ConsoleKey.F4), ref handled);
        Assert.True(handled);
        Assert.Same(layouts[1], runContext.AppConfig.DefaultLayout);

        runContext.AppConfig.DefaultLayout = layouts[^1];
        mainScreen.KeyPressed(Key(ConsoleKey.F4), ref handled);
        Assert.Same(layouts[0], runContext.AppConfig.DefaultLayout);
    }

    [Fact]
    public void Health_Is_Listed_Directly_Under_Summary()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();

        List<MenuListViewItem> items = mainScreen.GetControl<MenuControl>().MenuItems!;

        Assert.Equal("SUMMARY", items[0].Text);
        Assert.Equal("HEALTH", items[1].Text);
        Assert.IsType<HealthControl>(items[1].AssociatedControl);
    }

    // Regression test: the menu raises a click on every arrow key, even when Up can't move off the
    // first item, and each one reloaded and redrew SUMMARY - so holding Up redrew it on every repeat.
    [Fact]
    public void Holding_Up_On_The_First_Menu_Item_Does_Not_Reload_The_View()
    {
        // The built-in example tree's percentage charts draw the scale this looks for.
        runContext.AppConfig.DefaultLayout = null;

        (MainScreen2 mainScreen, _) = ShowMainScreen();
        runContextHelper.terminal.Invocations.Clear();

        for (int i = 0; i < 3; i++) {
            bool handled = false;
            mainScreen.KeyPressed(Key(ConsoleKey.UpArrow), ref handled);
        }

        Assert.DoesNotContain(SummaryChartScale, CapturedOutput());
    }

    private static ConsoleKeyInfo Key(ConsoleKey key) => new('\0', key, false, false, false);

    private List<SummaryControlLayout> LayoutsInSetupOrder() => runContext.AppConfig.Layouts.OrderBy(l => l.Name).ToList();

    [Fact]
    public void Left_And_Right_Move_Between_The_Health_Panes_Then_Back_To_The_Menu()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        HealthControl healthControl = mainScreen.GetControl<HealthControl>();
        bool          handled       = false;

        mainScreen.SetActiveControl(new MenuListViewItem(healthControl, "HEALTH"));
        mainScreen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);
        Assert.True(healthControl.CausesView.Focused);

        mainScreen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);
        Assert.True(healthControl.EventsView.Focused);

        mainScreen.KeyPressed(Key(ConsoleKey.LeftArrow), ref handled);
        Assert.True(healthControl.CausesView.Focused);

        mainScreen.KeyPressed(Key(ConsoleKey.LeftArrow), ref handled);
        Assert.True(mainScreen.GetControl<MenuControl>().Focused);
    }

    [Fact]
    public void Moving_Back_Up_To_Summary_Still_Shows_It()
    {
        // The built-in example tree's percentage charts draw the scale this looks for.
        runContext.AppConfig.DefaultLayout = null;

        (MainScreen2 mainScreen, _) = ShowMainScreen();

        bool handled = false;
        mainScreen.KeyPressed(Key(ConsoleKey.DownArrow), ref handled);
        runContextHelper.terminal.Invocations.Clear();

        handled = false;
        mainScreen.KeyPressed(Key(ConsoleKey.UpArrow), ref handled);

        Assert.Contains(SummaryChartScale, CapturedOutput());
    }

    private void RaiseSnapshotUpdated(SystemSnapshot snapshot)
    {
        FieldInfo? field = typeof(ServiceController).GetField(
            "SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance);

        MulticastDelegate? handler = (MulticastDelegate?)field!.GetValue(runContext.ServiceController);
        handler?.DynamicInvoke(runContext.ServiceController, new SystemSnapshotEventArgs(snapshot));
    }

    // Regression test: back from Setup, MainScreen2 reloads and re-themes its controls, which used
    // to overwrite the focus colour on the control that still held focus - so nothing looked
    // focused until the app was restarted.
    [Fact]
    public void Returning_From_Setup_Shows_The_Focus_Colour_On_The_Menu()
    {
        (MainScreen2 mainScreen, SetupScreen setupScreen) = ShowMainScreen();
        MenuControl menuControl = mainScreen.GetControl<MenuControl>();
        Assert.True(ShowsFocusBorder(menuControl));

        bool handled = false;
        mainScreen.KeyPressed(Key(ConsoleKey.F2), ref handled);

        // What ScreenApplication's loop does with an unhandled Escape: close Setup, show the screen under it.
        setupScreen.Close();
        mainScreen.Show();

        Assert.True(menuControl.Focused);
        Assert.True(ShowsFocusBorder(menuControl));
    }

    // Regression test: Setup re-applies its theme to every list on each draw, which used to
    // overwrite the focus colour a list's border had been switched to - so no list on the screen
    // ever showed focus.
    [Fact]
    public void Setup_Shows_The_Focus_Colour_On_The_Focused_List_As_Focus_Moves()
    {
        (MainScreen2 mainScreen, SetupScreen setupScreen) = ShowMainScreen();

        // The premise: an unfocused list's border is visibly different from a focused one's.
        Assert.NotEqual(
            Control.FocusSelectionColour.ToArgb(),
            runContext.AppConfig.Theme.ListViewBorderForeground.ToArgb());

        bool handled = false;
        mainScreen.KeyPressed(Key(ConsoleKey.F2), ref handled);

        ListView menuView = setupScreen.Controls.OfType<ListView>().Single(l => l.Focused);
        Assert.True(ShowsFocusBorder(menuView));

        handled = false;
        setupScreen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);

        ListView tabView = setupScreen.Controls.OfType<ListView>().Single(l => l.Focused);
        Assert.NotSame(menuView, tabView);
        Assert.True(ShowsFocusBorder(tabView));
        Assert.False(ShowsFocusBorder(menuView));
    }

    private (MainScreen2 Main, SetupScreen Setup) ShowMainScreen()
    {
        // A list border colour distinct from the focus colour (white by default), so a focused
        // border is visibly different. This AppConfig is this test's own.
        ConfigSection themeSection = new("Focus Test Theme");
        themeSection.Add(Constants.Keys.ListViewBorderForeground, "#123456");
        runContext.AppConfig.Theme.Update(themeSection);

        MainScreen2 mainScreen = new(runContext, screenApp) {
            Width = 120,
            Height = 40
        };

        SetupScreen setupScreen = new(runContext, screenApp);

        screenApp
            .RegisterScreen(mainScreen)
            .RegisterScreen(setupScreen);

        screenApp.ShowScreen<MainScreen2>();
        return (mainScreen, setupScreen);
    }

    // Read rather than set: Control.FocusSelectionColour is a process-wide static that other test
    // classes, running in parallel, read too.
    private static bool ShowsFocusBorder(Control control) =>
        control.DisplayBorderColour.ToArgb() == Control.FocusSelectionColour.ToArgb();

    [Fact]
    public void Tab_And_F2_Reach_The_End_Task_Confirmation_Instead_Of_The_Screen()
    {
        (MainScreen2 mainScreen, _) = ShowMainScreen();
        ProcessesControl processesControl = mainScreen.GetControl<ProcessesControl>();
        ProcessControl   processControl   = processesControl.ProcessControl;
        List<int>        ended            = [];
        bool             handled          = false;

        processControl.EndTask = ended.Add;
        mainScreen.SetActiveControl(new MenuListViewItem(processesControl, "PROCESSES"));
        mainScreen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);
        RaiseSnapshotUpdated(new SystemSnapshot {
            Processes = new ProcessInfo {
                Metrics = new ProcessMetrics {
                    Entries = [new ProcessEntry { Pid = 1111, ProcessName = "proc1111", CpuTimePercent = 1.0 }]
                }
            }
        });

        mainScreen.KeyPressed(new ConsoleKeyInfo('k', ConsoleKey.K, false, false, false), ref handled);
        Assert.True(processControl.IsEndTaskDialogVisible);

        runContextHelper.terminal.Invocations.Clear();
        mainScreen.KeyPressed(Key(ConsoleKey.F2), ref handled);
        mainScreen.KeyPressed(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false), ref handled);

        Assert.DoesNotContain("TASK MONITOR SETUP", CapturedOutput());
        Assert.True(processControl.IsEndTaskDialogVisible);

        mainScreen.KeyPressed(Key(ConsoleKey.Escape), ref handled);

        Assert.False(processControl.IsEndTaskDialogVisible);
        Assert.Empty(ended);
    }

    private List<Theme> ThemesInSetupOrder() => runContext.AppConfig.Themes.OrderBy(t => t.Name).ToList();

    [Fact]
    public void The_Footer_Takes_A_Second_Row_For_Tips_From_The_Active_View()
    {
        runContext.AppConfig.ShowTips = true;
        (MainScreen2 mainScreen, _) = ShowMainScreen();

        FooterControl footer     = mainScreen.GetControl<FooterControl>();
        Control       activeView = mainScreen.GetControl<SummaryControl2>();
        int           withTips   = activeView.Height;

        Assert.Equal(2, footer.Height);
        Assert.Equal(mainScreen.Height - 2, footer.Y);

        // What returning from Setup does: show this screen again, which lays it out afresh.
        runContext.AppConfig.ShowTips = false;
        mainScreen.Show();

        Assert.Equal(1, footer.Height);
        Assert.Equal(mainScreen.Height - 1, footer.Y);
        Assert.Equal(withTips + 1, activeView.Height);
    }

    [Fact]
    public void T_Is_Left_Alone_When_Tips_Are_Off()
    {
        runContext.AppConfig.ShowTips = false;
        (MainScreen2 mainScreen, _) = ShowMainScreen();

        FooterControl footer = mainScreen.GetControl<FooterControl>();
        footer.TipIndex      = 0;

        bool handled = false;
        mainScreen.KeyPressed(new ConsoleKeyInfo('t', ConsoleKey.T, false, false, false), ref handled);

        Assert.False(handled);
        Assert.Equal(0, footer.TipIndex);
    }

    [Fact]
    public void T_Shows_The_Next_Tip()
    {
        runContext.AppConfig.ShowTips = true;
        (MainScreen2 mainScreen, _) = ShowMainScreen();

        FooterControl footer = mainScreen.GetControl<FooterControl>();
        footer.TipIndex      = 0;

        bool handled = false;
        mainScreen.KeyPressed(new ConsoleKeyInfo('t', ConsoleKey.T, false, false, false), ref handled);

        Assert.True(handled);
        Assert.Equal(1, footer.TipIndex);
    }
}
