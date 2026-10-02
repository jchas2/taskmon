using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Screens;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui;

public sealed class MainScreen2Tests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;
    private readonly ScreenApplication screenApp;

    public MainScreen2Tests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
        screenApp = new ScreenApplication(runContext.Terminal);
    }

    // Read rather than set: Control.FocusSelectionColour is a process-wide static that other test
    // classes, running in parallel, read too.
    private static bool ShowsFocusBorder(Control control) =>
        control.DisplayBorderColour.ToArgb() == Control.FocusSelectionColour.ToArgb();

    private static ConsoleKeyInfo Key(ConsoleKey key) => new('\0', key, false, false, false);

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

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

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
}
