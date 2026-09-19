using Moq;
using Task.Monitor.Gui;
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
