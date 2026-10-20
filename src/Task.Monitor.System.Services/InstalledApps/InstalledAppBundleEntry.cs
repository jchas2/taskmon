namespace Task.Monitor.System.Services.InstalledApps;

public static class InstalledAppBundleEntry
{
    private const string AppStorePublisher = "Mac App Store";

    public static readonly IReadOnlyList<string> InfoKeys          = ["CFBundleShortVersionString", "CFBundleVersion", "NSHumanReadableCopyright"];
    public static readonly IReadOnlyList<string> LocalizedInfoKeys = ["CFBundleDisplayName", "NSHumanReadableCopyright"];

    private static string BundleFileName(string bundlePath) =>
        Path.GetFileNameWithoutExtension(bundlePath.TrimEnd('/'));

    private static string? FirstNonEmpty(IEnumerable<IReadOnlyDictionary<string, object?>?> sources, params string[] keys) =>
        sources.OfType<IReadOnlyDictionary<string, object?>>()
            .SelectMany(source => keys.Select(key => source.GetValueOrDefault(key) as string))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?
            .Trim();

    public static DateTime GetStamp(string bundlePath)
    {
        string infoPlist = Path.Combine(bundlePath, "Contents", "Info.plist");

        DateTime bundleWritten = Directory.GetLastWriteTimeUtc(bundlePath);
        DateTime infoWritten   = File.Exists(infoPlist) ? File.GetLastWriteTimeUtc(infoPlist) : DateTime.MinValue;

        return bundleWritten > infoWritten
            ? bundleWritten
            : infoWritten;
    }

    public static InstalledApp? Parse(
        IReadOnlyDictionary<string, object?>? info,
        IReadOnlyDictionary<string, object?>? localizedInfo,
        string                                bundlePath,
        InstalledAppScope                     scope,
        string?                               signingPublisher,
        DateTime?                             installDate,
        long?                                 estimatedSizeKb,
        bool                                  sizePending)
    {
        if (info is null) {
            return null;
        }

        string name = FirstNonEmpty([localizedInfo], "CFBundleDisplayName") ?? BundleFileName(bundlePath);

        if (string.IsNullOrWhiteSpace(name)) {
            return null;
        }

        return new InstalledApp {
            Name            = name,
            Version         = FirstNonEmpty([info], "CFBundleShortVersionString", "CFBundleVersion"),
            Publisher       = ResolvePublisher(info, localizedInfo, signingPublisher),
            InstallDate     = installDate,
            InstallLocation = bundlePath,
            EstimatedSizeKb = estimatedSizeKb,
            SizePending     = sizePending,
            Scope           = scope,
            Origin          = bundlePath
        };
    }

    private static string? ResolvePublisher(
        IReadOnlyDictionary<string, object?>  info,
        IReadOnlyDictionary<string, object?>? localizedInfo,
        string?                               signingPublisher) =>
        signingPublisher is null or AppStorePublisher
            ? CopyrightPublisher.Parse(FirstNonEmpty([info, localizedInfo], "NSHumanReadableCopyright")) ?? signingPublisher
            : signingPublisher;
}
