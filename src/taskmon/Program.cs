using System.Diagnostics;
using System.Reflection;
using Task.Monitor.System;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Process;
using Processor = Task.Monitor.Process.Processor;

namespace Task.Monitor;

class Program
{
    internal const int ExitSuccess = 0;
    internal const int ExitFailure = 1;
    internal const int UnhandledExceptionExitCode = 2;
    private const int DebugWait = 3000;
    
    private static void UnhandledErrorConsoleTidyUp()
    {
        Console.ResetColor();
        Console.CursorVisible = true;
    }

    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => {
            ExceptionHelper.HandleUnhandledException(eventArgs);
            UnhandledErrorConsoleTidyUp();
            Environment.Exit(UnhandledExceptionExitCode);
        };

        TaskScheduler.UnobservedTaskException += (sender, eventArgs) => {
            ExceptionHelper.HandleUnhandledException(new UnhandledExceptionEventArgs(eventArgs.Exception, isTerminating: true));
            UnhandledErrorConsoleTidyUp();
            eventArgs.SetObserved();
            Environment.Exit(UnhandledExceptionExitCode);
        };        
        
        Console.CancelKeyPress += (sender, args) => {
            Trace.WriteLine("Ctrl+C or Ctrl+Break signalled");
            UnhandledErrorConsoleTidyUp();
        };

        if (args.Any(arg => arg.Equals("--debug", StringComparison.CurrentCultureIgnoreCase))) {
            OutputWriter.Out.WriteLine($"Waiting for debugger attach to Pid {Environment.ProcessId}");
            
            while (!Debugger.IsAttached) {
                Thread.Sleep(DebugWait);
            }
            
            Debugger.Break();
        }
        
        // CI tooling relies on this switch, resolve early.
        if (args.Any(arg => arg.Equals("--version", StringComparison.CurrentCultureIgnoreCase))) {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "Unknown";
            Console.WriteLine($"{Constants.AppName} version {version}");
            return ExitSuccess;
        }

        ProcessService processService = new();
        SystemTerminal terminal = new();
        ModuleService moduleService = new();
        ThreadService threadService = new();
        FileSystem fileSystem = new();
        Processor processor = new(processService);
        AppConfig appConfig = new(fileSystem);

        try {
            RunContext runContext = new(
                fileSystem,
                terminal,
                processService,
                moduleService,
                threadService,
                processor,
                appConfig);

            TaskMonApp app = new(runContext);
            return app.Run(args);
        }
        catch (Exception e) {
            ExceptionHelper.HandleUnhandledException(new UnhandledExceptionEventArgs(e, isTerminating: true));
            UnhandledErrorConsoleTidyUp();
            Environment.Exit(UnhandledExceptionExitCode);
        }

        return ExitSuccess;
    }
}
