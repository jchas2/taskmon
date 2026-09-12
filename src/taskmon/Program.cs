using System.Diagnostics;
using System.Reflection;
using Task.Monitor.System;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Services;

namespace Task.Monitor;

class Program
{
    internal const int ExitSuccess = 0;
    internal const int ExitFailure = 1;
    private const int UnhandledExceptionExitCode = 2;
    private const int DebugWait = 3000;
    
    private static void RestoreConsole()
    {
        Console.CursorVisible = true;
        ConsoleEx.RestoreScreenBuffer();
    }

    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => {
            RestoreConsole();
        };
        
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => {
            ExceptionHelper.HandleUnhandledException(eventArgs);
            Environment.Exit(UnhandledExceptionExitCode);
        };

        TaskScheduler.UnobservedTaskException += (sender, eventArgs) => {
            ExceptionHelper.HandleUnhandledException(new UnhandledExceptionEventArgs(eventArgs.Exception, isTerminating: true));
            eventArgs.SetObserved();
            Environment.Exit(UnhandledExceptionExitCode);
        };        
        
        Console.CancelKeyPress += (sender, args) => {
            Trace.WriteLine("Ctrl+C or Ctrl+Break signalled");
            RestoreConsole();
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

        ServiceController serviceController = new();
        SystemTerminal terminal = new();
        FileSystem fileSystem = new();
        AppConfig appConfig = new(fileSystem);

        try {
            RunContext runContext = new(
                serviceController,
                fileSystem,
                terminal,
                appConfig);

            TaskMonApp app = new(runContext);
            return app.Run(args);
        }
        catch (Exception e) {
            ExceptionHelper.HandleUnhandledException(new UnhandledExceptionEventArgs(e, isTerminating: true));
            Environment.Exit(UnhandledExceptionExitCode);
        }
        
        return ExitSuccess;
    }
}
