using System.CommandLine;
using System.Reflection;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui;
using Task.Monitor.System;
using Task.Monitor.System.Screens;

namespace Task.Monitor;

public sealed class TaskMgrApp(RunContext runContext)
{
    private const string MutexId = "Task-Mon-d3f8e2a1-4b6f-4e8a-9b2d-1c3e4f5a6b7c";

    private static Mutex? mutex = null;

    private RootCommand InitRootCommand()
    {
        Option<int?> pidOption = new(
            name: "--pid",
            description: "Monitor the given PID.");
        Option<string> usernameOption = new(
            "--username", 
            "Monitor processes for the given username.");
        Option<string> processOption = new(
            "--process", 
            "Monitor processes matching or partially matching the given process name.");
        Option<Statistics?> sortOption = new(
            name: "--sort",
            description: "Sort the process display by sorting on the statistics column in descending order.");
        Option<bool?> ascendingOption = new(
            name: "--ascending", 
            description: "Sort the statistics column in ascending order.");
        Option<int> delayOption = new(
            name: "--delay",
            description: "Delay (in milliseconds) between process updates.");
        Option<int?> limitOption = new(
            name: "--limit",
            description: "Limit the number of iterations to execute before exiting.");
        Option<int?> nprocsOption = new(
            name: "--nprocs",
            description: "Only display up to nprocs processes.");
        Option<string> themeOption = new(
            name: "--theme",
            description: "Load a theme from the config file. Default themes are \"theme-colour\" and \"theme-mono\".");
        Option<bool?> debugOption = new(
            name: "--debug",
            description: "Pause execution on startup until a debugger is attached to the process.");

        RootCommand rootCommand = new("Task.Monitorfor the command line.") {
            pidOption,
            usernameOption,
            processOption,
            sortOption,
            ascendingOption,
            delayOption,
            limitOption,
            nprocsOption,
            themeOption,
            debugOption,
        };
        
        rootCommand.SetHandler(context =>
        {
            void AssignIfValid<T>(
                T? optionValue, 
                Action<T> assignmentAction, 
                Func<T, bool> validation) where T : struct
            {
                if (optionValue.HasValue && validation(optionValue.Value)) {
                    assignmentAction.Invoke(optionValue.Value);
                }
            }

            void AssignIfStringValid(string? optionValue, Action<string> assignmentAction)
            {
                if (!string.IsNullOrWhiteSpace(optionValue)) {
                    assignmentAction.Invoke(optionValue);
                }
            }            
            
            // First load configuration from disk.
            string? configPath = runContext.AppConfig.DefaultConfigFilePath;
            
            if (!string.IsNullOrEmpty(configPath)) {
                if (runContext.FileSystem.FileExists(configPath)) {
                    runContext.AppConfig.TryLoad(configPath);
                }
                else {
                    runContext.AppConfig.TrySave(configPath);
                }
            }
            
            // Only override what's in config if a value comes in from the command line.
            int? pid =               context.ParseResult.GetValueForOption(pidOption);
            string? userName =       context.ParseResult.GetValueForOption(usernameOption);
            string? process =        context.ParseResult.GetValueForOption(processOption);
            Statistics? sortColumn = context.ParseResult.GetValueForOption(sortOption);
            bool? sortAscending =    context.ParseResult.GetValueForOption(ascendingOption);
            int? delay =             context.ParseResult.GetValueForOption(delayOption);
            int? limit =             context.ParseResult.GetValueForOption(limitOption);
            int? nprocs =            context.ParseResult.GetValueForOption(nprocsOption);
            string? themeName =      context.ParseResult.GetValueForOption(themeOption);
            
            AssignIfValid(pid,    val => runContext.AppConfig.FilterPid = val,           val => val >= 0);
            AssignIfValid(limit,  val => runContext.AppConfig.IterationLimit = val,      val => val >= 0);
            AssignIfValid(nprocs, val => runContext.AppConfig.NumberOfProcesses = val,   val => val > 0);
            AssignIfValid(delay,  val => runContext.AppConfig.DelayInMilliseconds = val, val => val > 500);
            
            AssignIfStringValid(userName, val => runContext.AppConfig.FilterUserName = val);
            AssignIfStringValid(process,  val => runContext.AppConfig.FilterProcess = val);
            
            if (sortColumn.HasValue) {
                runContext.AppConfig.SortColumn = sortColumn.Value;
            }

            if (sortAscending.HasValue) {
                runContext.AppConfig.SortAscending = sortAscending.Value;
            }
            
            if (!string.IsNullOrWhiteSpace(themeName)) {
                Theme? defaultTheme = runContext.AppConfig.Themes
                    .FirstOrDefault(t => t.Name.Equals(themeName, StringComparison.CurrentCultureIgnoreCase));

                if (defaultTheme != null) {
                    runContext.AppConfig.DefaultTheme = defaultTheme;
                }
            }

            RunCommand(runContext);
        });   
        
        return rootCommand;
    }

    private static int RunCommand(RunContext runContext)
    {
        SystemTerminal terminal = new();
        ScreenApplication screenApp = new(terminal);

        Screen[] screens = {
            new MainScreen(screenApp, runContext),
            new HelpScreen(runContext),
            new SetupScreen(runContext),
            new AboutScreen(runContext)
        };

        foreach (Screen screen in screens) {
            screenApp.RegisterScreen(screen);
        }
        
        runContext.Processor.Delay = runContext.AppConfig.DelayInMilliseconds;
        runContext.Processor.IrixMode = runContext.AppConfig.UseIrixReporting;
        runContext.Processor.IterationLimit = runContext.AppConfig.IterationLimit;
        runContext.Processor.Run();

        screenApp.Run(screens[0]);
        
        runContext.Processor.Stop();
        
        return 0;
    }
    
    public int Run(string[] args)
    {
        bool createdMutex = true;

        try {
            mutex = new Mutex(initiallyOwned: false, name: MutexId, out createdMutex);

            if (!mutex.WaitOne(0, false)) {
                runContext.OutputWriter.WriteLine("Another instance of app is already running.".ToRed());
                return -1;
            }

            RootCommand rootCommand = InitRootCommand();
            int exitCode = rootCommand.Invoke(args);

            return exitCode;
        }
        finally {
            if (mutex != null) {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
        }
    }
}
