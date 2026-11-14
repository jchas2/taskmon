using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Resources;

namespace Task.Monitor.Actions;

public sealed class ShowUsageAction : IAction
{
    public int Run()
    {
        string text = string.Format(
            CultureInfo.CurrentCulture,
            Strings.CliUsage,
            Constants.AppName,
            AssemblyVersionInfo.GetVersion());

        OutputWriter.Out.WriteLine(text);
        return Program.ExitFailure;
    }
}
