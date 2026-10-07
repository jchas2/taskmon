#if __APPLE__
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Startup;

public partial class StartupService
{
    private const string MachineLaunchAgentsPath  = "/Library/LaunchAgents";
    private const string MachineLaunchDaemonsPath = "/Library/LaunchDaemons";
    private const string UserLaunchAgentsFolder   = "Library/LaunchAgents";
    private const string LaunchdOverridesPath     = "/private/var/db/com.apple.xpc.launchd";
    private const string ApplicationsPath         = "/Applications";
    private const string UserApplicationsFolder   = "Applications";
    private const string OpenAtLoginOrigin        = "Login Items (Open at Login)";
    private const string OpenAtLoginUnavailable   = "Open at Login needs Full Disk Access under sudo";

    private const uint NobodyUid = unchecked((uint)-2);

    private const string AppleLabelPrefix = "com.apple.";

    private readonly Dictionary<string, string?> publisherCache = new(StringComparer.Ordinal);

    private partial StartupSpecs ScanStartup()
    {
        publisherCache.Clear();

        StartupSpecs specs = new();
        StartupTargetUser user = StartupTargetUser.Current();

        LaunchdOverrides systemOverrides = ReadOverrides("disabled.plist");
        LaunchdOverrides userOverrides   = ReadOverrides($"disabled.{user.Uid}.plist");

        BackgroundItems? background = user.IsRoot
            ? BackgroundItems.From(BackgroundItemsDatabase.Read(), user)
            : null;

        if (background is not null) {
            ScanBackgroundItems(
                specs, 
                background, 
                userOverrides, 
                systemOverrides);
        }
        else {
            ScanOpenAtLoginItems(specs, user);

            foreach (string appPath in EnumerateApplications(user.HomeDirectory)) {
                ScanLoginHelpers(
                    specs, 
                    appPath, 
                    user, 
                    userOverrides);
                
                ScanBundledLaunchdJobs(
                    specs, 
                    appPath, 
                    user, 
                    userOverrides, 
                    systemOverrides);
            }
        }

        ScanLaunchdFolder(
            specs,
            Path.Combine(user.HomeDirectory, UserLaunchAgentsFolder),
            StartupEntrySource.LaunchAgent,
            StartupEntryScope.User,
            userOverrides,
            plistPath => background?.IsLegacyApproved(plistPath) ?? GetLegacyApproval(plistPath, user.IsSessionOwner));

        ScanLaunchdFolder(
            specs,
            MachineLaunchAgentsPath,
            StartupEntrySource.LaunchAgent,
            StartupEntryScope.Machine,
            userOverrides,
            plistPath => background?.IsLegacyApproved(plistPath) ?? GetLegacyApproval(plistPath, user.IsSessionOwner));

        ScanLaunchdFolder(
            specs,
            MachineLaunchDaemonsPath,
            StartupEntrySource.LaunchDaemon,
            StartupEntryScope.Machine,
            systemOverrides,
            plistPath => background?.IsLegacyApproved(plistPath) ?? GetLegacyApproval(plistPath, isAnswerable: true));

        return specs;
    }

    private void ScanBackgroundItems(
        StartupSpecs specs,
        BackgroundItems background,
        LaunchdOverrides userOverrides,
        LaunchdOverrides systemOverrides)
    {
        foreach (BackgroundItemRecord record in background.Records) {
            if (record.IsLegacy) {
                continue;
            }

            if (record.Is(BackgroundItemType.App)) {
                // A disabled app record is only the parent of embedded items, not Open at Login.
                if (record.IsApproved && record.ResolvePath(null) is { } appPath) {
                    specs.Entries.Add(CreateOpenAtLoginEntry(record.Name, appPath, background.FindDeveloperName(record)));
                }
            }
            else if (record.Is(BackgroundItemType.LoginItem)) {
                AddBackgroundLoginHelper(specs, background, record, userOverrides);
            }
            else if (record.Is(BackgroundItemType.Agent) || record.Is(BackgroundItemType.Daemon)) {
                AddBackgroundLaunchdJob(specs, background, record, userOverrides, systemOverrides);
            }
        }
    }

    private void AddBackgroundLoginHelper(
        StartupSpecs specs,
        BackgroundItems background,
        BackgroundItemRecord record,
        LaunchdOverrides userOverrides)
    {
        if (record.ResolvePath(background.FindParentPath(record)) is not { } helperPath) {
            return;
        }

        (string? bundleId, string? executable) = ReadBundleInfo(helperPath);
        
        string label = record.BundleIdentifier 
            ?? bundleId 
            ?? record.Name 
            ?? Path.GetFileNameWithoutExtension(helperPath);

        specs.Entries.Add(new StartupEntry {
            Name           = label,
            Command        = helperPath,
            ExecutablePath = executable ?? helperPath,
            Publisher      = ResolvePublisher(helperPath) ?? background.FindDeveloperName(record),
            Source         = StartupEntrySource.LoginHelper,
            Scope          = StartupEntryScope.User,
            State          = userOverrides.ResolveState(label, null, record.IsApproved),
            Origin         = helperPath
        });
    }

    private void AddBackgroundLaunchdJob(
        StartupSpecs specs,
        BackgroundItems background,
        BackgroundItemRecord record,
        LaunchdOverrides userOverrides,
        LaunchdOverrides systemOverrides)
    {
        string? parentPath = background.FindParentPath(record);

        if (record.ResolvePath(parentPath) is not { } plistPath ||
            LaunchdJobDefinition.Parse(PropertyList.ReadFile(plistPath), parentPath) is not { StartsAtLoad: true } definition) {

            return;
        }

        bool isDaemon = record.Is(BackgroundItemType.Daemon);
        
        string label = definition.Label 
            ?? record.Name 
            ?? Path.GetFileNameWithoutExtension(plistPath);
        
        LaunchdOverrides overrides = isDaemon 
            ? systemOverrides 
            : userOverrides;

        StartupEntry entry = CreateLaunchdEntry(
            definition,
            label,
            isDaemon ? StartupEntrySource.LaunchDaemon : StartupEntrySource.LaunchAgent,
            isDaemon ? StartupEntryScope.Machine : StartupEntryScope.User,
            plistPath);

        entry.Publisher ??= background.FindDeveloperName(record);
        entry.State = overrides.ResolveState(label, definition.Disabled, record.IsApproved);

        specs.Entries.Add(entry);
    }

    private void ScanOpenAtLoginItems(StartupSpecs specs, StartupTargetUser user)
    {
        if (!user.IsSessionOwner) {
            specs.Notes.Add(OpenAtLoginUnavailable);
            return;
        }

        foreach (LaunchServices.LoginItem item in LaunchServices.GetSessionLoginItems()) {
            specs.Entries.Add(item.Path is { Length: > 0 } path
                ? CreateOpenAtLoginEntry(item.Name, path, null)
                : new StartupEntry {
                    Name   = item.Name,
                    Source = StartupEntrySource.OpenAtLogin,
                    Scope  = StartupEntryScope.User,
                    State  = StartupEntryState.Enabled,
                    Origin = OpenAtLoginOrigin
                });
        }
    }

    private StartupEntry CreateOpenAtLoginEntry(string? name, string appPath, string? developerName) =>
        new() {
            Name           = name ?? Path.GetFileNameWithoutExtension(appPath),
            Command        = appPath,
            ExecutablePath = ReadBundleInfo(appPath).Executable ?? appPath,
            Publisher      = ResolvePublisher(appPath) ?? developerName,
            Source         = StartupEntrySource.OpenAtLogin,
            Scope          = StartupEntryScope.User,
            State          = StartupEntryState.Enabled,
            Origin         = OpenAtLoginOrigin
        };

    private void ScanLaunchdFolder(
        StartupSpecs specs,
        string folderPath,
        StartupEntrySource source,
        StartupEntryScope scope,
        LaunchdOverrides overrides,
        Func<string, bool?> getApproval)
    {
        foreach (string file in EnumerateFiles(folderPath, "*.plist")) {
            string fileLabel = Path.GetFileNameWithoutExtension(file);

            if (fileLabel.StartsWith(AppleLabelPrefix, StringComparison.Ordinal)) {
                continue;
            }

            LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(PropertyList.ReadFile(file));

            if (definition is null) {
                specs.Entries.Add(new StartupEntry {
                    Name   = fileLabel,
                    Source = source,
                    Scope  = scope,
                    State  = StartupEntryState.Unknown,
                    Origin = file
                });

                continue;
            }

            string label = definition.Label ?? fileLabel;

            if (!definition.StartsAtLoad || label.StartsWith(AppleLabelPrefix, StringComparison.Ordinal)) {
                continue;
            }

            StartupEntry entry = CreateLaunchdEntry(
                definition, 
                label, 
                source, 
                scope, 
                file);
            
            entry.State = overrides.ResolveState(label, definition.Disabled, getApproval(file));

            specs.Entries.Add(entry);
        }
    }

    private void ScanLoginHelpers(
        StartupSpecs specs,
        string appPath,
        StartupTargetUser user,
        LaunchdOverrides userOverrides)
    {
        string loginItemsPath = Path.Combine(
            appPath, 
            "Contents", 
            "Library", 
            "LoginItems");

        foreach (string helperPath in EnumerateDirectories(loginItemsPath, "*.app")) {
            (string? bundleId, string? executable) = ReadBundleInfo(helperPath);

            if (bundleId is null || !IsRegistered(bundleId, userOverrides, IsLoadedInUserDomain(user))) {
                continue;
            }

            specs.Entries.Add(new StartupEntry {
                Name           = bundleId,
                Command        = helperPath,
                ExecutablePath = executable ?? helperPath,
                Publisher      = ResolvePublisher(helperPath),
                Source         = StartupEntrySource.LoginHelper,
                Scope          = StartupEntryScope.User,
                State          = userOverrides.ResolveState(bundleId, null),
                Origin         = helperPath
            });
        }
    }

    private void ScanBundledLaunchdJobs(
        StartupSpecs specs,
        string appPath,
        StartupTargetUser user,
        LaunchdOverrides userOverrides,
        LaunchdOverrides systemOverrides)
    {
        string libraryPath = Path.Combine(appPath, "Contents", "Library");

        ScanBundledLaunchdFolder(
            specs,
            appPath,
            Path.Combine(libraryPath, "LaunchAgents"),
            StartupEntrySource.LaunchAgent,
            StartupEntryScope.User,
            userOverrides,
            IsLoadedInUserDomain(user));

        ScanBundledLaunchdFolder(
            specs,
            appPath,
            Path.Combine(libraryPath, "LaunchDaemons"),
            StartupEntrySource.LaunchDaemon,
            StartupEntryScope.Machine,
            systemOverrides,
            ServiceManagement.IsLoadedInSystemDomain);
    }

    private void ScanBundledLaunchdFolder(
        StartupSpecs specs,
        string appPath,
        string folderPath,
        StartupEntrySource source,
        StartupEntryScope scope,
        LaunchdOverrides overrides,
        Func<string, bool> isLoaded)
    {
        foreach (string file in EnumerateFiles(folderPath, "*.plist")) {
            LaunchdJobDefinition? definition = LaunchdJobDefinition.Parse(PropertyList.ReadFile(file), appPath);

            if (definition is not { StartsAtLoad: true, Label: { } label } || !IsRegistered(label, overrides, isLoaded)) {
                continue;
            }

            StartupEntry entry = CreateLaunchdEntry(
                definition, 
                label, 
                source, 
                scope, 
                file);
            
            entry.State = overrides.ResolveState(label, definition.Disabled);

            specs.Entries.Add(entry);
        }
    }

    private StartupEntry CreateLaunchdEntry(
        LaunchdJobDefinition definition,
        string label,
        StartupEntrySource source,
        StartupEntryScope scope,
        string plistPath)
    {
        StartupEntry entry = new() {
            Name           = label,
            Command        = definition.Command,
            ExecutablePath = definition.ExecutablePath,
            Arguments      = definition.Arguments,
            Source         = source,
            Scope          = scope,
            Origin         = plistPath
        };

        if (definition.ExecutablePath is { } executablePath) {
            entry.Publisher = ResolvePublisher(executablePath);
        }

        return entry;
    }

    private static Func<string, bool> IsLoadedInUserDomain(StartupTargetUser user) =>
        user.IsSessionOwner
            ? ServiceManagement.IsLoadedInUserDomain
            : static _ => false;

    private static bool IsRegistered(string label, LaunchdOverrides overrides, Func<string, bool> isLoaded) =>
        overrides.Contains(label) || isLoaded(label);

    private static bool? GetLegacyApproval(string plistPath, bool isAnswerable)
    {
        if (!isAnswerable) {
            return null;
        }

        return ServiceManagement.GetLegacyStatus(plistPath) switch {
            ServiceManagement.LegacyServiceStatus.Enabled          => true,
            ServiceManagement.LegacyServiceStatus.RequiresApproval => false,
            _                                                      => null
        };
    }

    private static LaunchdOverrides ReadOverrides(string fileName) =>
        LaunchdOverrides.Parse(PropertyList.ReadFile(Path.Combine(LaunchdOverridesPath, fileName)));

    private static (string? BundleId, string? Executable) ReadBundleInfo(string appPath)
    {
        if (PropertyList.ReadFile(Path.Combine(appPath, "Contents", "Info.plist")) is not
            IReadOnlyDictionary<string, object?> info) {

            return (null, null);
        }

        string? bundleId = info.GetValueOrDefault("CFBundleIdentifier") as string;
        
        string? executable = info.GetValueOrDefault("CFBundleExecutable") is string { Length: > 0 } name
            ? Path.Combine(appPath, "Contents", "MacOS", name)
            : null;

        return (string.IsNullOrEmpty(bundleId) ? null : bundleId, executable);
    }

    private static IEnumerable<string> EnumerateApplications(string home)
    {
        foreach (string root in new[] { ApplicationsPath, Path.Combine(home, UserApplicationsFolder) }) {
            foreach (string directory in EnumerateDirectories(root, "*")) {
                if (directory.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) {
                    yield return directory;
                    continue;
                }

                foreach (string nested in EnumerateDirectories(directory, "*.app")) {
                    yield return nested;
                }
            }
        }
    }

    private static string[] EnumerateFiles(string folderPath, string pattern)
    {
        if (!Directory.Exists(folderPath)) {
            return [];
        }

        try {
            return Directory.GetFiles(folderPath, pattern);
        }
        catch (Exception ex) {
            TraceEx.WriteLineOnce($"{nameof(EnumerateFiles)} {folderPath}", ex.Message);
            return [];
        }
    }

    private static string[] EnumerateDirectories(string folderPath, string pattern)
    {
        if (!Directory.Exists(folderPath)) {
            return [];
        }

        try {
            return Directory.GetDirectories(folderPath, pattern);
        }
        catch (Exception ex) {
            TraceEx.WriteLineOnce($"{nameof(EnumerateDirectories)} {folderPath}", ex.Message);
            return [];
        }
    }

    private string? ResolvePublisher(string path)
    {
        if (publisherCache.TryGetValue(path, out string? cached)) {
            return cached;
        }

        string? publisher = CodeSigningPublisher.Parse(CodeSigning.GetSigningCertificateSummary(path));
        publisherCache[path] = publisher;

        return publisher;
    }

    private sealed class BackgroundItems
    {
        private readonly Dictionary<string, BackgroundItemRecord> byIdentifier = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> legacyApprovalByPath = new(StringComparer.Ordinal);

        public List<BackgroundItemRecord> Records { get; } = new();

        public static BackgroundItems? From(BackgroundItemsDatabase? database, StartupTargetUser user)
        {
            if (database is null || user.Uuid is null) {
                return null;
            }

            BackgroundItems items = new();
            items.Records.AddRange(database.RecordsFor(user.Uuid));
            items.Records.AddRange(database.RecordsFor(Membership.GetUserUuid(NobodyUid)));

            foreach (BackgroundItemRecord record in items.Records) {
                if (record.Identifier is { } identifier) {
                    items.byIdentifier.TryAdd(identifier, record);
                }

                if (record.IsLegacy && record.ResolvePath(null) is { } plistPath) {
                    items.legacyApprovalByPath[plistPath] = record.IsApproved;
                }
            }

            return items;
        }

        public bool? IsLegacyApproved(string plistPath) =>
            legacyApprovalByPath.TryGetValue(plistPath, out bool approved) ? approved : null;

        public string? FindParentPath(BackgroundItemRecord record) =>
            FindParent(record)?.ResolvePath(null);

        public string? FindDeveloperName(BackgroundItemRecord record)
        {
            BackgroundItemRecord? current = record;

            for (int depth = 0; current is not null && depth < 4; depth++) {
                if (!string.IsNullOrEmpty(current.DeveloperName)) {
                    return current.DeveloperName;
                }

                if (current.Is(BackgroundItemType.Developer) && !string.IsNullOrEmpty(current.Name)) {
                    return current.Name;
                }

                current = FindParent(current);
            }

            return null;
        }

        private BackgroundItemRecord? FindParent(BackgroundItemRecord record) =>
            record.ParentIdentifier is { } parent && byIdentifier.TryGetValue(parent, out BackgroundItemRecord? found)
                ? found
                : null;
    }
}
#endif
