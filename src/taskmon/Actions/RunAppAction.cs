using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Actions;

public sealed class RunAppAction(RunContext runContext) : IAction
{
    public int Run()
    {
        ConsoleEx.SetAlternateScreenBuffer();

        ScreenApplication screenApp = new(runContext.Terminal);
        MainScreen mainScreen = new(runContext, screenApp);

        screenApp
            .RegisterScreen(mainScreen)
            .RegisterScreen(new HelpScreen(runContext))
            .RegisterScreen(new SetupScreen(runContext))
            .RegisterScreen(new AboutScreen(runContext));
        
        runContext.Processor.Delay = runContext.AppConfig.DelayInMilliseconds;
        runContext.Processor.IrixMode = runContext.AppConfig.UseIrixReporting;
        runContext.Processor.IterationLimit = runContext.AppConfig.IterationLimit;
        runContext.Processor.Run();

        // Run the App event loop.
        screenApp.Run(mainScreen);
        runContext.Processor.Stop();
        return Program.ExitSuccess;
    }
}