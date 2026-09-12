#if __WIN32__
using Microsoft.Win32;
#endif
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Startup;

#pragma warning disable CA1416 // Validate platform compatibility

public partial class StartupService
{
#if __WIN32__
    private const string RunKeyPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnceKeyPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string ApprovedRunPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedRun32Path =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
    private const string ApprovedFolderPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    // Publisher lookups open and read a file's version resource, so the answer is memoised for the
    // duration of one scan (many entries under one vendor's install folder share a target).
    private readonly Dictionary<string, string?> publisherCache =
        new(StringComparer.OrdinalIgnoreCase);

    private partial StartupSpecs ScanStartup()
    {
        publisherCache.Clear();

        StartupSpecs specs = new();

        ScanRunKeys(specs);
        ScanStartupFolders(specs);
        ScanScheduledTasks(specs);

        specs.Entries.Sort(static (left, right) => {
            int bySource = left.Source.CompareTo(right.Source);

            return bySource != 0
                ? bySource
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });

        return specs;
    }

    // ---- Registry Run / RunOnce ----------------------------------------------------------------

    private void ScanRunKeys(StartupSpecs specs)
    {
        ScanRunKey(specs, RegistryHive.LocalMachine, RegistryView.Registry64,
            StartupEntryScope.Machine, RunKeyPath, ApprovedRunPath, StartupEntrySource.RunKey);
        ScanRunKey(specs, RegistryHive.LocalMachine, RegistryView.Registry64,
            StartupEntryScope.Machine, RunOnceKeyPath, ApprovedRunPath, StartupEntrySource.RunOnceKey);

        // The 32-bit registry view is a distinct set of keys only on a 64-bit OS; on 32-bit
        // Windows it aliases the same key and would double every entry.
        if (Environment.Is64BitOperatingSystem) {
            ScanRunKey(specs, RegistryHive.LocalMachine, RegistryView.Registry32,
                StartupEntryScope.Machine, RunKeyPath, ApprovedRun32Path, StartupEntrySource.RunKey);
            ScanRunKey(specs, RegistryHive.LocalMachine, RegistryView.Registry32,
                StartupEntryScope.Machine, RunOnceKeyPath, ApprovedRun32Path, StartupEntrySource.RunOnceKey);
        }

        ScanRunKey(specs, RegistryHive.CurrentUser, RegistryView.Default,
            StartupEntryScope.User, RunKeyPath, ApprovedRunPath, StartupEntrySource.RunKey);
        ScanRunKey(specs, RegistryHive.CurrentUser, RegistryView.Default,
            StartupEntryScope.User, RunOnceKeyPath, ApprovedRunPath, StartupEntrySource.RunOnceKey);
    }

    private void ScanRunKey(
        StartupSpecs specs,
        RegistryHive hive,
        RegistryView view,
        StartupEntryScope scope,
        string keyPath,
        string approvedPath,
        StartupEntrySource source)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? runKey = baseKey.OpenSubKey(keyPath);

        if (runKey is null) {
            return;
        }

        using RegistryKey? approvedKey = baseKey.OpenSubKey(approvedPath);

        string origin = $"{HiveShortName(hive)}\\{keyPath}";

        foreach (string valueName in runKey.GetValueNames()) {
            if (string.IsNullOrEmpty(valueName)) {
                continue; // the (Default) value is not a startup entry
            }

            string rawCommand = runKey.GetValue(
                valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? string.Empty;

            if (rawCommand.Length == 0) {
                continue;
            }

            StartupEntry entry = new() {
                Name = valueName,
                Command = Environment.ExpandEnvironmentVariables(rawCommand),
                Source = source,
                Scope = scope,
                Origin = origin
            };

            (entry.State, entry.DisabledOnUtc) = ReadApprovedState(approvedKey, valueName);
            PopulateTarget(entry, rawCommand);

            specs.Entries.Add(entry);
        }
    }

    // ---- Startup folders ---------------------------------------------------------------------

    private void ScanStartupFolders(StartupSpecs specs)
    {
        using RegistryKey approvedBase =
            RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        using RegistryKey? approvedKey = approvedBase.OpenSubKey(ApprovedFolderPath);

        ScanStartupFolder(specs, Environment.SpecialFolder.CommonStartup, StartupEntryScope.Machine, approvedKey);
        ScanStartupFolder(specs, Environment.SpecialFolder.Startup, StartupEntryScope.User, approvedKey);
    }

    private void ScanStartupFolder(
        StartupSpecs specs,
        Environment.SpecialFolder folder,
        StartupEntryScope scope,
        RegistryKey? approvedKey)
    {
        string folderPath = Environment.GetFolderPath(folder);

        if (folderPath.Length == 0 || !Directory.Exists(folderPath)) {
            return;
        }

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
                Name = Path.GetFileNameWithoutExtension(file),
                Source = StartupEntrySource.StartupFolder,
                Scope = scope,
                Origin = folderPath
            };

            if (Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) {
                PopulateFromShortcut(entry, file);
            }
            else {
                entry.ExecutablePath = file;
                entry.Command = file;
                entry.Publisher = ResolvePublisher(file);
            }

            (entry.State, entry.DisabledOnUtc) = ReadApprovedState(approvedKey, fileName);

            specs.Entries.Add(entry);
        }
    }

    private void PopulateFromShortcut(StartupEntry entry, string shortcutPath)
    {
        if (ShellLink.Resolve(shortcutPath) is not { } target) {
            entry.Command = shortcutPath;
            return;
        }

        entry.ExecutablePath = target.Path;
        entry.Arguments = target.Arguments.Length > 0 ? target.Arguments : null;
        entry.Command = target.Arguments.Length > 0
            ? $"\"{target.Path}\" {target.Arguments}"
            : target.Path;
        entry.Publisher = ResolvePublisher(target.Path);
    }

    // ---- Scheduled tasks -----------------------------------------------------------------------

    // Windows registers dozens of its own maintenance tasks (backup, diagnostics, provisioning,
    // language components, ...) with Logon or Boot triggers under this folder. They are OS
    // plumbing, not applications starting themselves up, so they are excluded the way Autoruns'
    // "hide Microsoft entries" option would - by folder rather than a signature check, since taskmon
    // has no Authenticode verification interop.
    private const string BuiltInWindowsTaskFolderPrefix = @"\Microsoft\Windows\";

    // Only tasks with a Logon or Boot trigger are shown - those are the ones that actually run at
    // startup, the same signal Run/RunOnce/StartupFolder entries share by construction. Tasks whose
    // only triggers are time/event/idle-based belong to the wider Task Scheduler, not "why does
    // something start when I log in".
    private void ScanScheduledTasks(StartupSpecs specs)
    {
        foreach (ScheduledTasks.TaskRecord record in ScheduledTasks.EnumerateTasks()) {
            if (record.FolderPath.StartsWith(BuiltInWindowsTaskFolderPrefix, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            ScheduledTaskDefinition? definition = ScheduledTaskDefinition.Parse(record.Xml);

            if (definition is null || !(definition.HasLogonTrigger || definition.HasBootTrigger)) {
                continue;
            }

            StartupEntry entry = new() {
                Name = record.Name,
                Source = StartupEntrySource.ScheduledTask,
                Scope = ResolveScheduledTaskScope(definition),
                Origin = record.FolderPath,
                State = record.Enabled ? StartupEntryState.Enabled : StartupEntryState.Disabled
            };

            if (definition.ExecutablePath is { Length: > 0 } executablePath) {
                entry.ExecutablePath = executablePath;
                entry.Arguments = definition.Arguments;
                entry.Command = definition.Arguments is { Length: > 0 }
                    ? $"\"{executablePath}\" {definition.Arguments}"
                    : executablePath;
                entry.Publisher = ResolvePublisher(executablePath) ?? definition.Author;
            }
            else {
                entry.Publisher = definition.Author;
            }

            specs.Entries.Add(entry);
        }
    }

    // A boot trigger fires before any specific user logs in, so it is a Machine entry regardless of
    // the account it runs as. A logon trigger scoped to the current user is a User entry; one that
    // fires for any user (an empty UserId) or for a different named user is treated as Machine, the
    // same way the all-users Startup folder is.
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

    // ---- Shared helpers --------------------------------------------------------------------

    private static (StartupEntryState State, DateTime? DisabledOnUtc) ReadApprovedState(
        RegistryKey? approvedKey, string valueName)
    {
        // No StartupApproved record means the OS runs the entry, which Task Manager shows as
        // Enabled.
        return approvedKey?.GetValue(valueName) is byte[] blob
            ? StartupApprovedState.Parse(blob)
            : (StartupEntryState.Enabled, null);
    }

    private void PopulateTarget(StartupEntry entry, string rawCommand)
    {
        (string path, string arguments) = StartupCommandLine.Split(rawCommand);

        if (path.Length == 0) {
            return;
        }

        entry.ExecutablePath = path;
        entry.Arguments = arguments.Length > 0 ? arguments : null;
        entry.Publisher = ResolvePublisher(path);
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

    private static string HiveShortName(RegistryHive hive) => hive switch {
        RegistryHive.LocalMachine => "HKLM",
        RegistryHive.CurrentUser => "HKCU",
        _ => hive.ToString()
    };
#endif
}

#pragma warning restore CA1416 // Validate platform compatibility
