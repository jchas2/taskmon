#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Common;
#endif

namespace Task.Monitor.System.Services.InstalledApps;

public partial class InstalledAppsService
{
#if __APPLE__
    private const string ApplicationsPath       = "/Applications";
    private const string SystemApplicationsPath = "/System/Applications";
    private const string UserApplicationsFolder = "Applications";

    private readonly Dictionary<string, (DateTime Stamp, string? Publisher)> publisherCache = new(StringComparer.Ordinal);

    private static DateTime ResolveInstallDate(string appPath) =>
        Attr.GetAddedTime(appPath)?.ToLocalTime() ?? Directory.GetCreationTime(appPath);

    // Reading a code signature is the slowest part of a scan, so publishers are cached against the
    // bundle's stamp and only re-read for apps that changed since the last scan.
    private string? ResolvePublisher(string appPath)
    {
        DateTime stamp = InstalledAppBundleEntry.GetStamp(appPath);

        if (publisherCache.TryGetValue(appPath, out (DateTime Stamp, string? Publisher) cached) && cached.Stamp == stamp) {
            return cached.Publisher;
        }

        string? publisher = CodeSigningPublisher.Parse(CodeSigning.GetSigningCertificateSummary(appPath));
        publisherCache[appPath] = (stamp, publisher);

        return publisher;
    }

    private void ScanApplications(
        InstalledAppsSpecs specs,
        string[]           roots,
        InstalledAppScope  scope)
    {
        foreach (string appPath in AppBundle.EnumerateApplications(roots)) {
            bool sizeKnown = sizeCache.TryGetCached(appPath, out long? sizeKb);

            InstalledApp? app = InstalledAppBundleEntry.Parse(
                info:             AppBundle.ReadInfo(appPath, InstalledAppBundleEntry.InfoKeys),
                localizedInfo:    AppBundle.ReadLocalizedInfo(appPath, InstalledAppBundleEntry.LocalizedInfoKeys),
                bundlePath:       appPath,
                scope:            scope,
                signingPublisher: ResolvePublisher(appPath),
                installDate:      ResolveInstallDate(appPath),
                estimatedSizeKb:  sizeKb,
                sizePending:      !sizeKnown);

            if (app is not null) {
                specs.Apps.Add(app);
            }
        }
    }

    private partial InstalledAppsSpecs ScanInstalledApps()
    {
        InstalledAppsSpecs specs = new();
        TargetUser         user  = TargetUser.Current();

        ScanApplications(
            specs,
            [ApplicationsPath, SystemApplicationsPath],
            InstalledAppScope.Machine);

        ScanApplications(
            specs,
            [Path.Combine(user.HomeDirectory, UserApplicationsFolder)],
            InstalledAppScope.User);

        specs.Apps.Sort(static (left, right) =>
            string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

        return specs;
    }
#endif
}
