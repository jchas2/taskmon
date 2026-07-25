using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;

namespace Task.Monitor.Actions;

public sealed class ThemeHelpAction(RunContext runContext) : IAction
{
    public int Run()
    {
        foreach (Theme theme in runContext.AppConfig.Themes) {
            OutputWriter.Out.WriteLine(theme.Name);
        }

        return Program.ExitSuccess;
    }
}
