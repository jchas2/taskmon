using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;

namespace Task.Monitor.Actions;

public sealed class ShowUsageAction : IAction
{
    public int Run()
    {
        string text = $@"
{Constants.AppName} version {AssemblyVersionInfo.GetVersion()}

Usage: {Constants.AppName} [options]

Options:
     --pid PID            Monitor the given process PID
  -u --username <NAME>    Only show processes for the the given user NAME
  -p --process <NAME>     Only show processes with matching process NAME
  -s --sort <COLUMN>      Sort by COLUMN in the process list view
     --sort-help          Displays the list of columns available for the --sort option
  -d --delay <DELAY>      DELAY (in milliseconds) between process list view updates
  -l --limit <LIMIT>      Limit the number of process updates to LIMIT before stopping
     --nprocs <NUMPROCS>  Only display the top NUMPROCS in the process list view
  -t --theme <NAME>       Load theme NAME from the config file
     --theme-help         Displays the list of available theme names for the --theme option

     --debug              Pause execution on startup until a debugger attaches to the {Constants.AppName} process
  -v --version            Print version information
  -i --info               Print configuration and environment information.
  -h --help               Print help

Press F1 inside {Constants.AppName} for online help. 
";
        OutputWriter.Out.WriteLine(text);
        return Program.ExitFailure;
    }
}