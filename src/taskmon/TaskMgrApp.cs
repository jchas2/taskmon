using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

    private class Options
    {
        public required Option<int?> Pid { get; set; }
        public required Option<string?> UserName { get; set; }
        public required Option<string?> Process { get; set; }
        public required Option<Statistics?> Sort { get; set; }
        public required Option<bool?> Ascending { get; set; }
        public required Option<int?> Delay { get; set; }
        public required Option<int?> Limit { get; set; }
        public required Option<int?> NProcs { get; set; }
        public required Option<string?> Theme { get; set; }
        public required Option<bool?> Debug { get; set; }
    }
    
    private RootCommand InitRootCommand()
    {
        Options options = new() {
            Pid = new Option<int?>( 
                name: "--pid",
                description: "Monitor the given PID."),
            UserName = new Option<string?>(
                "--username", 
                "Monitor processes for the given username."),
            Process = new Option<string?>(
                "--process", 
                "Monitor processes matching or partially matching the given process name."),
            Sort = new Option<Statistics?>(
                name: "--sort",
                description: "Sort the process display by sorting on the statistics column in descending order."),
            Ascending = new Option<bool?>(
                name: "--ascending", 
                description: "Sort the statistics column in ascending order."),
            Delay = new Option<int?>(
                name: "--delay",
                description: "Delay (in milliseconds) between process updates."),
            Limit = new Option<int?>(
                name: "--limit",
                description: "Limit the number of iterations to execute before exiting."),
            NProcs = new Option<int?>(
                name: "--nprocs",
                description: "Only display up to nprocs processes."),
            Theme = new Option<string?>(
                name: "--theme",
                description: "Load a theme from the config file. Default themes are \"theme-colour\" and \"theme-mono\"."),
            Debug = new Option<bool?>(
                name: "--debug",
                description: "Pause execution on startup until a debugger is attached to the process.")
        };

        RootCommand rootCommand = new("Task Monitor for the command line.") {
            options.Pid,
            options.UserName,
            options.Process,
            options.Sort,
            options.Ascending,
            options.Delay,
            options.Limit,
            options.NProcs,
            options.Theme,
            options.Debug
        };
        
        rootCommand.SetHandler(context => {
            try {
                SetHandlerInternal(context, options);
            }
            catch (Exception ex) {
                ExceptionHelper.HandleUnhandledException(new UnhandledExceptionEventArgs(ex, isTerminating: true));
                Console.ResetColor();
                Console.CursorVisible = true;
                Environment.Exit(Program.UnhandledExceptionExitCode);
            }
        });   
        
        return rootCommand;
    }

    private void SetHandlerInternal(InvocationContext context, Options options)
    {
            void AssignIfValid<T>(
                string optionName,
                T? optionValue, 
                Action<T> assignmentAction, 
                Func<T, bool> validation) where T : struct
            {
                if (optionValue.HasValue && validation(optionValue.Value)) {
                    Trace.WriteLine($"Option override {optionName} = {optionValue.Value}");
                    assignmentAction.Invoke(optionValue.Value);
                }
            }

            void AssignIfStringValid(string optionName, string? optionValue, Action<string> assignmentAction)
            {
                if (!string.IsNullOrWhiteSpace(optionValue)) {
                    Trace.WriteLine($"Option override {optionName} = {optionValue}");
                    assignmentAction.Invoke(optionValue);
                }
            }            

            string? logPath = runContext.AppConfig.DefaultLogPath;
            
            if (!string.IsNullOrEmpty(logPath)) {
                if (runContext.FileSystem.DirectoryExists(logPath)) {
                    FormattedTextWriterTraceListener.Initialise(
                        logPath, 
                        maxBytes: 2 * 1024 * 1024, 
                        maxFiles: 16,
                        Constants.AppName);
                }
            }

            SystemStatistics stats = new();
            SystemInfo.GetSystemInfo(ref stats);
            
            Trace.WriteLine($"{Constants.AppName} started.");
            Trace.WriteLine($"{Constants.AppName} version = {AssemblyVersionInfo.GetVersion()}");
            Trace.WriteLine($"Running as root = {SystemInfo.IsRunningAsRoot()}");
            Trace.WriteLine($"CPU = {stats.CpuName}");
            Trace.WriteLine($"OS Version = {stats.OsVersion}");
            Trace.WriteLine($"Log Path = {logPath ?? string.Empty}");
            Trace.WriteLine(new string('-', 10));

            Trace.WriteLine($"{TerminalCapabilities.ColourModeEnvVar} = {Environment.GetEnvironmentVariable(TerminalCapabilities.ColourModeEnvVar)}");
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Trace.WriteLine($"WT_SESSION = {Environment.GetEnvironmentVariable("WT_SESSION") ?? string.Empty}");
            }
            else {
                Trace.WriteLine($"TERM = {Environment.GetEnvironmentVariable("TERM") ?? string.Empty}");
                Trace.WriteLine($"TERM_PROGRAM = {Environment.GetEnvironmentVariable("TERM_PROGRAM") ?? string.Empty}");
                Trace.WriteLine($"COLORTERM = {Environment.GetEnvironmentVariable("COLORTERM") ?? string.Empty}");
            }

            Trace.WriteLine($"Host is modern terminal = {TerminalCapabilities.IsModernTerminal(Environment.GetEnvironmentVariable)}");
            Trace.WriteLine(new string('-', 10));
            
            string? configPath = runContext.AppConfig.DefaultConfigFilePath;
            
            // Resolve AppConfig from disk. If this fails, AppConfig is designed to work with in-memory defaults.
            if (!string.IsNullOrEmpty(configPath)) {
                if (runContext.FileSystem.FileExists(configPath)) {
                    Trace.WriteLine($"Loading config from {configPath}.");
                    _ = runContext.AppConfig.TryLoad(configPath);
                }
                else {
                    Trace.WriteLine($"Saving initial config to {configPath}.");
                    _ = runContext.AppConfig.TrySave(configPath);
                }

                string configStr = runContext.AppConfig.ToString();
                Trace.WriteLine($"Config = \n{configStr}");
            }
            else {
                Trace.WriteLine($"Unresolved config path, running with default in-memory AppConfig.");
            }

            // Only override what's in config if a value comes in from the command line.
            int? pid =               context.ParseResult.GetValueForOption(options.Pid);
            string? userName =       context.ParseResult.GetValueForOption(options.UserName);
            string? process =        context.ParseResult.GetValueForOption(options.Process);
            Statistics? sortColumn = context.ParseResult.GetValueForOption(options.Sort);
            bool? sortAscending =    context.ParseResult.GetValueForOption(options.Ascending);
            int? delay =             context.ParseResult.GetValueForOption(options.Delay);
            int? limit =             context.ParseResult.GetValueForOption(options.Limit);
            int? nprocs =            context.ParseResult.GetValueForOption(options.NProcs);
            string? themeName =      context.ParseResult.GetValueForOption(options.Theme);
            
            AssignIfValid(nameof(pid),    pid,    val => runContext.AppConfig.FilterPid = val,           val => val >= 0);
            AssignIfValid(nameof(limit),  limit,  val => runContext.AppConfig.IterationLimit = val,      val => val >= 0);
            AssignIfValid(nameof(nprocs), nprocs, val => runContext.AppConfig.NumberOfProcesses = val,   val => val > 0);
            AssignIfValid(nameof(delay),  delay,  val => runContext.AppConfig.DelayInMilliseconds = val, val => val > 500);
            
            AssignIfStringValid(nameof(userName), userName, val => runContext.AppConfig.FilterUserName = val);
            AssignIfStringValid(nameof(process),  process,  val => runContext.AppConfig.FilterProcess = val);
            
            if (sortColumn.HasValue) {
                runContext.AppConfig.SortColumn = sortColumn.Value;
                Trace.WriteLine($"Option {nameof(runContext.AppConfig.SortColumn)} = {sortColumn.Value}");
            }

            if (sortAscending.HasValue) {
                runContext.AppConfig.SortAscending = sortAscending.Value;
                Trace.WriteLine($"Option {nameof(runContext.AppConfig.SortAscending)} = {sortAscending.Value}");
            }
            
            if (!string.IsNullOrWhiteSpace(themeName)) {
                Trace.WriteLine($"Option {nameof(themeName)} = {themeName}");
                
                Theme? defaultTheme = runContext.AppConfig.Themes
                    .FirstOrDefault(t => t.Name.Equals(themeName, StringComparison.CurrentCultureIgnoreCase));

                if (defaultTheme != null) {
                    runContext.AppConfig.DefaultTheme = defaultTheme;
                }
                else {
                    Trace.WriteLine($"Theme {themeName} not found in resolved themes.");
                }
            }

            RunCommand(runContext);
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
