using System.Diagnostics;
using System.Reflection;
using System.Text;
using Task.Monitor.System;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Process;
using Processor = Task.Monitor.Process.Processor;

namespace Task.Monitor;

class Program
{
    internal const int UnhandledExceptionExitCode = 1;
    private const int DebugWait = 3000;
    
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => {
            ExceptionHelper.HandleUnhandledException(eventArgs);
            Environment.Exit(UnhandledExceptionExitCode);
        };

        TaskScheduler.UnobservedTaskException += (sender, eventArgs) => {
            ExceptionHelper.HandleUnhandledException(new UnhandledExceptionEventArgs(eventArgs.Exception, isTerminating: true));
            eventArgs.SetObserved();
        };        
        
        Console.CancelKeyPress += (sender, args) => {
            Console.ResetColor();
            Console.CursorVisible = true;
        };

        using TerminalUtf8Encoder _ = new();
        Console.OutputEncoding = Encoding.UTF8;

        using TerminalColourRestorer __ = new();
        
        if (args.Any(arg => arg.Equals("--debug", StringComparison.CurrentCultureIgnoreCase))) {
            OutputWriter.Out.WriteLine($"Waiting for debugger attach to Pid {Environment.ProcessId}");
            
            while (false == Debugger.IsAttached) {
                Thread.Sleep(DebugWait);
            }
            
            Debugger.Break();
        }
        
        if (args.Any(arg => arg.Equals("--version", StringComparison.CurrentCultureIgnoreCase))) {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "Unknown";
            Console.WriteLine($"{Constants.AppName} version {version}");
            return 0;
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
                appConfig,
                outputWriter: null);

            TaskMgrApp app = new(runContext);
            return app.Run(args);
        }
        catch (Exception e) {
            ExceptionHelper.HandleUnhandledException(new UnhandledExceptionEventArgs(e, isTerminating: true));
            Console.ResetColor();
            Console.CursorVisible = true;
            Environment.Exit(UnhandledExceptionExitCode);
        }

        return 0;
    }
}
