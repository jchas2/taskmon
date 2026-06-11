#if DEBUG
//    #define DEBUG_TRACE_LISTENER 
#endif

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
    private const int UnhandledExceptionExitCode = 1;
    private const int DebugWait = 3000;
    
    private static void HandleException(UnhandledExceptionEventArgs ev)
    {
        if (ev.IsTerminating) {
            OutputWriter.Error.WriteLine(
                Environment.NewLine + "Runtime has encountered a fatal unhandled Exception.".ToRed());
        }
        
        if (ev.ExceptionObject is Exception e) {
            OutputWriter.Error.WriteLine(e.GetType().ToString().ToRed());
            OutputWriter.Error.WriteLine(e.Message.ToRed());
            OutputWriter.Error.WriteLine(e.StackTrace?? e.ToString().ToYellow());
            Debug.WriteLine(e.ToString());
            return;
        }
        
        string unhandledError = $"Unhandled error: {ev.ExceptionObject.GetType().Name}";
        OutputWriter.Error.WriteLine(unhandledError.ToRed());
        Debug.WriteLine(unhandledError);
    }
    
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => {
            HandleException(eventArgs);
            Environment.Exit(UnhandledExceptionExitCode);
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
            Console.WriteLine($"taskmon version {version}");
            return 0;
        }
        
#if DEBUG_TRACE_LISTENER
        FormattedTextWriterTraceListener.Initialise();
#endif
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
            HandleException(new UnhandledExceptionEventArgs(e, isTerminating: true));
            Console.ResetColor();
            Console.CursorVisible = true;
            Environment.Exit(UnhandledExceptionExitCode);
        }

        return 0;
    }
}
