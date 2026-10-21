#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.LaunchdServices;

internal static class LaunchdServiceLookup
{
    internal const string SystemDomainGroup = "System domain";
    internal const string UserDomainGroup   = "User domain";

    private const string ApplicationLabelPrefix = "application.";
    private const string UserLaunchAgentsFolder = "Library/LaunchAgents";
    private const string UserLaunchAgentsGroup  = "~/Library/LaunchAgents";

    private static readonly string[] SystemFolders = ["/System/Library/LaunchDaemons", "/Library/LaunchDaemons"];
    private static readonly string[] UserFolders   = ["/System/Library/LaunchAgents", "/Library/LaunchAgents"];

    internal readonly record struct Plist(string Path, string Group, LaunchdJobDefinition? Definition);

    private static LaunchdServiceInfo CreateService(
        string                                label,
        LaunchdDomain                         domain,
        string                                group,
        LaunchdJobDefinition?                 definition,
        LaunchdStartType                      startType,
        LaunchdOverrides                      overrides,
        IReadOnlyDictionary<string, object?>? job,
        bool                                  jobsKnown)
    {
        LaunchdServiceInfo service = new() {
            Label          = label,
            Domain         = domain,
            Group          = group,
            StartType      = startType,
            Disabled       = overrides.IsDisabled(label, definition?.Disabled),
            ProgramPath    = definition?.ExecutablePath,
            Arguments      = definition?.Arguments,
            Pid            = job?.GetValueOrDefault("PID") is long pid ? (int)pid : null,
            LastExitStatus = job?.GetValueOrDefault("LastExitStatus") is long status ? (int)status : null
        };

        service.Status = (jobsKnown, job, service.Pid) switch {
            (false, _, _) => LaunchdServiceStatus.Unknown,
            (_, null, _)        => LaunchdServiceStatus.NotLoaded,
            (_, _, not null)    => LaunchdServiceStatus.Running,
                                 _ => LaunchdServiceStatus.Waiting
        };

        return service;
    }

    internal static List<LaunchdServiceInfo> GetServices(LaunchdScanCache cache)
    {
        TargetUser user = TargetUser.Current();

        List<LaunchdServiceInfo> services = Merge(
            LaunchdDomain.System,
            ReadPlists(cache, SystemFolders.Select(folder => (folder, folder))),
            ServiceManagement.GetSystemJobs(),
            LaunchdOverrides.ReadSystem());

        services.AddRange(Merge(
            LaunchdDomain.User,
            ReadPlists(cache, [
                .. UserFolders.Select(folder => (folder, folder)),
                (Path.Combine(user.HomeDirectory, UserLaunchAgentsFolder), UserLaunchAgentsGroup)
            ]),
            user.IsSessionOwner ? ServiceManagement.GetUserJobs() : null,
            LaunchdOverrides.ReadUser(user.Uid)));

        foreach (LaunchdServiceInfo service in services) {
            if (service.ProgramPath is { } path) {
                service.Publisher = cache.GetPublisher(path);
            }
        }

        cache.Prune();

        return services;
    }

    internal static List<LaunchdServiceInfo> Merge(
        LaunchdDomain                                        domain,
        IReadOnlyList<Plist>                                 plists,
        IReadOnlyList<IReadOnlyDictionary<string, object?>>? jobs,
        LaunchdOverrides                                     overrides)
    {
        Dictionary<string, IReadOnlyDictionary<string, object?>> jobsByLabel = new(StringComparer.Ordinal);

        foreach (IReadOnlyDictionary<string, object?> job in jobs ?? []) {
            if (job.GetValueOrDefault("Label") is string { Length: > 0 } label) {
                jobsByLabel.TryAdd(label, job);
            }
        }

        HashSet<string>          listed   = new(StringComparer.Ordinal);
        List<LaunchdServiceInfo> services = new(plists.Count);

        foreach (Plist plist in plists) {
            string label = plist.Definition?.Label ?? Path.GetFileNameWithoutExtension(plist.Path);
            listed.Add(label);

            LaunchdServiceInfo service = CreateService(
                label,
                domain,
                plist.Group,
                plist.Definition,
                ToStartType(plist.Definition),
                overrides,
                jobsByLabel.GetValueOrDefault(label),
                jobs is not null);

            service.PlistPath = plist.Path;
            services.Add(service);
        }

        foreach ((string label, IReadOnlyDictionary<string, object?> job) in jobsByLabel) {
            if (listed.Contains(label) || label.StartsWith(ApplicationLabelPrefix, StringComparison.Ordinal)) {
                continue;
            }

            services.Add(CreateService(
                label,
                domain,
                domain == LaunchdDomain.System ? SystemDomainGroup : UserDomainGroup,
                LaunchdJobDefinition.Parse(job),
                job.GetValueOrDefault("OnDemand") is false ? LaunchdStartType.KeepAlive : LaunchdStartType.OnDemand,
                overrides,
                job,
                jobsKnown: true));
        }

        return services;
    }

    private static List<Plist> ReadPlists(LaunchdScanCache cache, IEnumerable<(string Folder, string Group)> folders)
    {
        List<Plist> plists = new();

        foreach ((string folder, string group) in folders) {
            foreach (string path in LaunchdJobDefinition.EnumeratePlists(folder)) {
                plists.Add(new Plist(path, group, cache.GetDefinition(path)));
            }
        }

        return plists;
    }

    internal static LaunchdStartType ToStartType(LaunchdJobDefinition? definition) => definition switch {
        { KeepAlive: true } => LaunchdStartType.KeepAlive,
        { RunAtLoad: true } => LaunchdStartType.AtLoad,
                          _ => LaunchdStartType.OnDemand
    };
}
#endif
