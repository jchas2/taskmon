#if __WIN32__
using System.Buffers.Binary;
using Microsoft.Win32;
#endif
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Startup;

#pragma warning disable CA1416 // Validate platform compatibility

public partial class StartupService
{
#if __WIN32__
    private const string RunKeyPath         = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnceKeyPath     = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string ApprovedRunPath    = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedRun32Path  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
    private const string ApprovedFolderPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    private static readonly long MaxFileTime = DateTime.MaxValue.ToFileTimeUtc();

    private readonly Dictionary<string, string?> publisherCache = new(StringComparer.OrdinalIgnoreCase);

    private const string BuiltInWindowsTaskFolderPrefix = @"\Microsoft\Windows\";

    private const string SoftwareKeyPrefix = @"SOFTWARE\";
    private const string Wow64KeyPrefix    = @"SOFTWARE\WOW6432Node\";

    internal static string? ApprovedRunPathFor(RegistryHive hive, RegistryView view, StartupEntrySource source) =>
        source != StartupEntrySource.RunKey                                  ? null              :
        hive == RegistryHive.LocalMachine && view == RegistryView.Registry32 ? ApprovedRun32Path :
                                                                               ApprovedRunPath;

    internal static RegistryHive ApprovedStartupFolderHive(StartupEntryScope scope) =>
        scope == StartupEntryScope.Machine
            ? RegistryHive.LocalMachine
            : RegistryHive.CurrentUser;

    private static bool LooksLikeExistingFile(string path)
    {
        try {
            return File.Exists(path) || File.Exists(path + ".exe");
        }
        catch {
            return false;
        }
    }

    private static RegistryKey? OpenApprovedKey(RegistryHive hive, string approvedPath)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        return baseKey.OpenSubKey(approvedPath);
    }

    internal static (StartupEntryState State, DateTime? DisabledOnUtc) ParseApprovedState(ReadOnlySpan<byte> blob)
    {
        // Parses the binary value Explorer writes under
        //   HK{CU,LM}\...\CurrentVersion\Explorer\StartupApproved\{Run, Run32, StartupFolder}
        if (blob.Length < 12) {
            return (StartupEntryState.Unknown, null);
        }

        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(blob);
        
        if ((flags & 0x1) == 0) {
            return (StartupEntryState.Enabled, null);
        }

        long fileTime = BinaryPrimitives.ReadInt64LittleEndian(blob[4..]);

        DateTime? disabledOn = fileTime > 0 && fileTime <= MaxFileTime
            ? DateTime.FromFileTimeUtc(fileTime)
            : null;

        return (StartupEntryState.Disabled, disabledOn);
    }

    private void PopulateFromShortcut(StartupEntry entry, string shortcutPath)
    {
        if (ShellLink.Resolve(shortcutPath, out ComFailure? failure) is not { } target) {
            scanNotes.Add($"shortcut {Path.GetFileName(shortcutPath)} not resolved, {failure}");
            entry.Command = shortcutPath;
            return;
        }

        entry.ExecutablePath = target.Path;
        entry.Arguments      = target.Arguments.Length > 0 ? target.Arguments : null;
        entry.Command        = target.Arguments.Length > 0
            ? $"\"{target.Path}\" {target.Arguments}"
            : target.Path;
        entry.Publisher      = ResolvePublisher(target.Path);
    }

    private void PopulateTarget(StartupEntry entry, string rawCommand)
    {
        (string path, string arguments) = SplitCommandLine(rawCommand);

        if (path.Length == 0) {
            return;
        }

        entry.ExecutablePath = path;
        entry.Arguments      = arguments.Length > 0 ? arguments : null;
        entry.Publisher      = ResolvePublisher(path);
    }

    private static (StartupEntryState State, DateTime? DisabledOnUtc) ReadApprovedState(
        RegistryKey? approvedKey, 
        string valueName)
    {
        return approvedKey?.GetValue(valueName) is byte[] blob
            ? ParseApprovedState(blob)
            : (StartupEntryState.Enabled, null);
    }

    private string? ResolvePublisher(string executablePath)
    {
        if (publisherCache.TryGetValue(executablePath, out string? cached)) {
            return cached;
        }

        string? publisher = WinVer.GetCompanyName(executablePath);
        publisherCache[executablePath] = publisher;

        return publisher;
    }

    private static StartupEntryScope ResolveScheduledTaskScope(ScheduledTaskDefinition definition)
    {
        if (definition.HasBootTrigger) {
            return StartupEntryScope.Machine;
        }

        string? userId = definition.LogonTriggerUserId;

        if (userId is null) {
            return StartupEntryScope.Machine;
        }

        string bareUserId = userId.Contains('\\') ? userId[(userId.LastIndexOf('\\') + 1)..] : userId;

        return string.Equals(bareUserId, Environment.UserName, StringComparison.OrdinalIgnoreCase)
            ? StartupEntryScope.User
            : StartupEntryScope.Machine;
    }

    internal static string RunKeyOrigin(RegistryHive hive, RegistryView view, string keyPath)
    {
        string path = hive == RegistryHive.LocalMachine
                      && view == RegistryView.Registry32
                      && keyPath.StartsWith(SoftwareKeyPrefix, StringComparison.OrdinalIgnoreCase)
            ? Wow64KeyPrefix + keyPath[SoftwareKeyPrefix.Length..]
            : keyPath;

        return $"{Win32StringHelper.HiveShortName(hive)}\\{path}";
    }

    private void ScanRunKey(
        StartupSpecs       specs,
        RegistryHive       hive,
        RegistryView       view,
        StartupEntryScope  scope,
        string             keyPath,
        StartupEntrySource source)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? runKey = baseKey.OpenSubKey(keyPath);

        if (runKey is null) {
            return;
        }

        string? approvedPath = ApprovedRunPathFor(hive, view, source);

        using RegistryKey? approvedKey = approvedPath is null ? null : OpenApprovedKey(hive, approvedPath);

        string origin = RunKeyOrigin(hive, view, keyPath);

        foreach (string valueName in runKey.GetValueNames()) {
            if (string.IsNullOrEmpty(valueName)) {
                // the (Default) value is not a startup entry.
                continue; 
            }

            string rawCommand = runKey.GetValue(
                valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? string.Empty;

            if (rawCommand.Length == 0) {
                continue;
            }

            StartupEntry entry = new() {
                Name     = valueName,
                Command  = Environment.ExpandEnvironmentVariables(rawCommand),
                Source   = source,
                Scope    = scope,
                Origin   = origin,
                Approval = approvedPath is null ? null : new StartupApproval(hive, approvedPath, valueName)
            };

            (entry.State, entry.DisabledOnUtc) = ReadApprovedState(approvedKey, valueName);
            PopulateTarget(entry, rawCommand);

            specs.Entries.Add(entry);
        }
    }

    private void ScanRunKeys(StartupSpecs specs)
    {
        ScanRunKey(
            specs, 
            RegistryHive.LocalMachine, 
            RegistryView.Registry64,
            StartupEntryScope.Machine, 
            RunKeyPath, 
            StartupEntrySource.RunKey);
        
        ScanRunKey(
            specs, 
            RegistryHive.LocalMachine, 
            RegistryView.Registry64,
            StartupEntryScope.Machine, 
            RunOnceKeyPath, 
            StartupEntrySource.RunOnceKey);

        if (Environment.Is64BitOperatingSystem) {
            ScanRunKey(
                specs, 
                RegistryHive.LocalMachine, 
                RegistryView.Registry32,
                StartupEntryScope.Machine, 
                RunKeyPath, 
                StartupEntrySource.RunKey);
            
            ScanRunKey(
                specs, 
                RegistryHive.LocalMachine, 
                RegistryView.Registry32,
                StartupEntryScope.Machine, 
                RunOnceKeyPath, 
                StartupEntrySource.RunOnceKey);
        }

        ScanRunKey(
            specs, 
            RegistryHive.CurrentUser, 
            RegistryView.Default,
            StartupEntryScope.User, 
            RunKeyPath, 
            StartupEntrySource.RunKey);
        
        ScanRunKey(
            specs, 
            RegistryHive.CurrentUser, 
            RegistryView.Default,
            StartupEntryScope.User, 
            RunOnceKeyPath, 
            StartupEntrySource.RunOnceKey);
    }

    private void ScanScheduledTasks(StartupSpecs specs)
    {
        IReadOnlyList<ScheduledTasks.TaskRecord> records = ScheduledTasks.EnumerateTasks(out ComFailure? failure);

        scanNotes.Add(failure is { } failed
            ? $"Task Scheduler FAILED, {failed}"
            : $"Task Scheduler OK, {records.Count} tasks read");

        foreach (ScheduledTasks.TaskRecord record in records) {
            if (record.FolderPath.StartsWith(BuiltInWindowsTaskFolderPrefix, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            ScheduledTaskDefinition? definition = ScheduledTaskDefinition.Parse(record.Xml);

            if (definition is null || !(definition.HasLogonTrigger || definition.HasBootTrigger)) {
                continue;
            }

            StartupEntry entry = new() {
                Name   = record.Name,
                Source = StartupEntrySource.ScheduledTask,
                Scope  = ResolveScheduledTaskScope(definition),
                Origin = record.FolderPath,
                State  = record.Enabled ? StartupEntryState.Enabled : StartupEntryState.Disabled
            };

            if (definition.ExecutablePath is { Length: > 0 } executablePath) {
                entry.ExecutablePath = executablePath;
                entry.Arguments      = definition.Arguments;
                entry.Command        = definition.Arguments is { Length: > 0 }
                    ? $"\"{executablePath}\" {definition.Arguments}"
                    : executablePath;
                entry.Publisher      = ResolvePublisher(executablePath) ?? definition.Author;
            }
            else {
                entry.Publisher = definition.Author;
            }

            specs.Entries.Add(entry);
        }
    }

    private partial StartupSpecs ScanStartup()
    {
        publisherCache.Clear();

        StartupSpecs specs = new();

        ScanRunKeys(specs);
        ScanStartupFolders(specs);
        ScanScheduledTasks(specs);

        return specs;
    }

    private void ScanStartupFolder(
        StartupSpecs              specs,
        Environment.SpecialFolder folder,
        StartupEntryScope         scope)
    {
        string folderPath = Environment.GetFolderPath(folder);

        if (folderPath.Length == 0 || !Directory.Exists(folderPath)) {
            return;
        }

        RegistryHive approvedHive = ApprovedStartupFolderHive(scope);

        using RegistryKey? approvedKey = OpenApprovedKey(approvedHive, ApprovedFolderPath);

        string[] files;

        try {
            files = Directory.GetFiles(folderPath);
        }
        catch (Exception ex) {
            TraceEx.WriteLineOnce($"{nameof(ScanStartupFolder)} {folderPath}", ex.Message);
            return;
        }

        foreach (string file in files) {
            string fileName = Path.GetFileName(file);

            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            StartupEntry entry = new() {
                Name     = Path.GetFileNameWithoutExtension(file),
                Source   = StartupEntrySource.StartupFolder,
                Scope    = scope,
                Origin   = folderPath,
                Approval = new StartupApproval(approvedHive, ApprovedFolderPath, fileName)
            };

            if (Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) {
                PopulateFromShortcut(entry, file);
            }
            else {
                entry.ExecutablePath = file;
                entry.Command        = file;
                entry.Publisher      = ResolvePublisher(file);
            }

            (entry.State, entry.DisabledOnUtc) = ReadApprovedState(approvedKey, fileName);
            specs.Entries.Add(entry);
        }
    }

    private void ScanStartupFolders(StartupSpecs specs)
    {
        ScanStartupFolder(
            specs,
            Environment.SpecialFolder.CommonStartup,
            StartupEntryScope.Machine);

        ScanStartupFolder(
            specs,
            Environment.SpecialFolder.Startup,
            StartupEntryScope.User);
    }

    internal static (string Path, string Arguments) SplitCommandLine(string? command)
    {
        string expanded = Environment.ExpandEnvironmentVariables(command?.Trim() ?? string.Empty);

        if (expanded.Length == 0) {
            return (string.Empty, string.Empty);
        }

        if (expanded[0] == '"') {
            int closingQuote = expanded.IndexOf('"', 1);

            return closingQuote > 0
                ? (expanded[1..closingQuote], expanded[(closingQuote + 1)..].TrimStart())
                : (expanded[1..], string.Empty);
        }

        int searchFrom = 0;

        while (true) {
            int space = expanded.IndexOf(' ', searchFrom);

            if (space < 0) {
                break;
            }

            string candidate = expanded[..space];

            if (LooksLikeExistingFile(candidate)) {
                return (candidate, expanded[(space + 1)..].TrimStart());
            }

            searchFrom = space + 1;
        }

        int firstSpace = expanded.IndexOf(' ');

        return firstSpace < 0
            ? (expanded, string.Empty)
            : (expanded[..firstSpace], expanded[(firstSpace + 1)..].TrimStart());
    }
#endif
}

#pragma warning restore CA1416 // Validate platform compatibility
