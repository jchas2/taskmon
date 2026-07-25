using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;

namespace Task.Monitor.Actions;

public sealed class SortHelpAction : IAction 
{
    public int Run()
    {
        var columns = Enum.GetNames<Statistics>()
            .OrderBy(name => name, StringComparer.Ordinal);
        
        foreach (string? column in columns) {
            OutputWriter.Out.WriteLine(column);
        }

        return Program.ExitSuccess;
    }
}