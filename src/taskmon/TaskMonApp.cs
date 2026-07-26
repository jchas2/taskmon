using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Task.Monitor.Actions;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;

namespace Task.Monitor;

public sealed class TaskMonApp(RunContext runContext)
{
    private void InitialiseProcess()
    {
        string? logPath = runContext.AppConfig.DefaultLogPath;
        
        if (!string.IsNullOrEmpty(logPath)) {
            if (runContext.FileSystem.DirectoryExists(logPath)) {
                FormattedTextWriterTraceListener.Initialise(
                    logPath, 
                    maxBytes: 2 * 1024 * 1024, 
                    maxFiles: 32,
                    Constants.AppName);
            }
        }

        SystemStatistics stats = new();
        _ = SystemInfo.GetSystemInfo(ref stats);
        
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
    }
    
    internal bool ProcessArgs(string[] args, out List<IAction> actions)
    {
        actions = new List<IAction>();
        bool result = true;
        
        string? ExtractArg(Func<string ,bool> predicate) => 
            args.Select((arg, index) => new { arg, index })
                .Where(param => predicate.Invoke(param.arg))
                .Select(param => args.ElementAtOrDefault(param.index + 1))
                .FirstOrDefault();

        string? pidArg     = ExtractArg(arg => arg == "--pid");
        string? userArg    = ExtractArg(arg => arg == "-u" || arg == "--username");
        string? processArg = ExtractArg(arg => arg == "-p" || arg == "--process");
        string? sortArg    = ExtractArg(arg => arg == "-s" || arg == "--sort");
        string? delayArg   = ExtractArg(arg => arg == "-d" || arg == "--delay");
        string? limitArg   = ExtractArg(arg => arg == "-l" || arg == "--limit");
        string? nprocsArg  = ExtractArg(arg => arg == "--nprocs");
        string? themeArg   = ExtractArg(arg => arg == "-t" || arg == "--theme");

        if (!string.IsNullOrEmpty(pidArg)) {
            if (int.TryParse(pidArg, out int pid)) {
                runContext.AppConfig.FilterPid = pid;
            }
            else {
                OutputWriter.Error.WriteLine($"{Constants.AppName}: bad pid arg: {pidArg}");
                result = false;
            }
        }

        if (!string.IsNullOrEmpty(userArg)) {
            runContext.AppConfig.FilterUserName = userArg;
        }

        if (!string.IsNullOrEmpty(processArg)) {
            runContext.AppConfig.FilterProcess = processArg;
        }

        if (!string.IsNullOrEmpty(sortArg)) {
            if (Enum.TryParse(sortArg, out Statistics sortCol)) {
                runContext.AppConfig.SortColumn = sortCol;
                runContext.AppConfig.VisibleColumns |= sortCol;
            }
            else {
                OutputWriter.Error.WriteLine($"{Constants.AppName}: bad sort arg: {sortArg}");
                result = false;
            }
        }
        
        if (!string.IsNullOrEmpty(delayArg)) {
            if (int.TryParse(delayArg, out int delay) && delay >= 500) {
                runContext.AppConfig.DelayInMilliseconds = delay;
            }
            else {
                OutputWriter.Error.WriteLine($"{Constants.AppName}: bad delay arg: {delayArg}");
                result = false;
            }
        }

        if (!string.IsNullOrEmpty(limitArg)) {
            if (int.TryParse(limitArg, out int limit) && limit >= 0) {
                runContext.AppConfig.IterationLimit = limit;
            }
            else {
                OutputWriter.Error.WriteLine($"{Constants.AppName}: bad limit arg: {limitArg}");
                result = false;
            }
        }

        if (!string.IsNullOrEmpty(nprocsArg)) {
            if (int.TryParse(nprocsArg, out int nprocs) && nprocs > 0) {
                runContext.AppConfig.NumberOfProcesses = nprocs;
            }
            else {
                OutputWriter.Error.WriteLine($"{Constants.AppName}: bad nprocs arg: {nprocsArg}");
                result = false;
            }
        }

        if (!string.IsNullOrEmpty(themeArg)) {
            Theme? defaultTheme =
                runContext.AppConfig.Themes.FirstOrDefault(t =>
                    t.Name.Equals(themeArg, StringComparison.CurrentCultureIgnoreCase));

            if (defaultTheme != null) {
                runContext.AppConfig.DefaultTheme = defaultTheme;
            }
            else {
                OutputWriter.Error.WriteLine($"{Constants.AppName}: bad theme arg: {themeArg}");
                result = false;
            }
        }

        bool isGpuOnly = args.Contains("--gpu-only");
        bool isCpuOnly = !isGpuOnly && args.Contains("--cpu-only");
        
        if (isGpuOnly || isCpuOnly) {
            var (layoutName, sortCol) = isGpuOnly
                ? (Constants.Sections.LayoutGpuAndGpuMemoryLarge, Statistics.Gpu)
                : (Constants.Sections.LayoutCpuAndMemoryLarge, Statistics.Cpu);
            
            Layout? defaultLayout = runContext.AppConfig.Layouts.FirstOrDefault(l =>
                    l.Name.Equals(layoutName, StringComparison.CurrentCultureIgnoreCase));

            if (defaultLayout != null) {
                runContext.AppConfig.DefaultLayout = defaultLayout;
                runContext.AppConfig.SortColumn = sortCol;
            }
            else {
                OutputWriter.Error.WriteLine($"{Constants.AppName}: Layout {layoutName} not found");
                result = false;
            }
        }
        
        if (!result || args.Any(arg => arg == "-h" || arg == "--help")) {
            actions.Add(new ShowUsageAction());
            return result;
        }

        if (result && args.Any(arg => arg == "--sort-help")) {
            actions.Add(new SortHelpAction());
        }

        if (result && args.Any(arg => arg == "--theme-help")) {
            actions.Add(new ThemeHelpAction(runContext));
        }

        if (result && !actions.Any()) {
            actions.Add(new RunAppAction(runContext));
        }
        
        return result;
    }
    
    public int Run(string[] args)
    {
        int exitCode = Program.ExitSuccess;
        string? terminalColourMode = Environment.GetEnvironmentVariable(TerminalCapabilities.ColourModeEnvVar);

        if (string.IsNullOrEmpty(terminalColourMode)) {
            Environment.SetEnvironmentVariable(TerminalCapabilities.ColourModeEnvVar, $"{ColourMode.Auto}");
        }
        
        using TerminalRestorer _ = new();
        Console.OutputEncoding = Encoding.UTF8;
        
        InitialiseProcess();
        
        if (!ProcessArgs(args, out List<IAction> actions)) {
            return Program.ExitFailure;
        }

        foreach (IAction action in actions) {
            Trace.WriteLine($"Invoking action {action.GetType()}");
            exitCode = action.Run();
            
            if (exitCode != Program.ExitSuccess) {
                return exitCode;
            }
        }

        return exitCode;
    }
}
