using Task.Monitor.Gui;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Tests.Gui.Commands;

public static class CommandHelper
{
    public static MainScreen SetupMainScreen() => SetupMainScreenWithAll().mainScreen;
    
    public static (RunContext context, MainScreen mainScreen) SetupMainScreenWithContext()
    {
        (RunContext context, ScreenApplication _, MainScreen mainScreen) = SetupMainScreenWithAll();
        return (context, mainScreen);
    }

    public static (ScreenApplication screenApp, MainScreen mainScreen) SetupMainScreenWithScreenApp()
    {
        (RunContext _, ScreenApplication screenApp, MainScreen mainScreen) = SetupMainScreenWithAll();
        return (screenApp, mainScreen);
    }

    private static (RunContext context, ScreenApplication screenApp, MainScreen mainScreen) SetupMainScreenWithAll()
    {
        RunContext context = new RunContextHelper().GetRunContext();
        ScreenApplication screenApp = new(context.Terminal);
        MainScreen mainScreen = new(context, screenApp);

        return (context, screenApp, mainScreen);
    }
}
