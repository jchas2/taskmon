#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Drivers;

internal static partial class DriverLookup
{
    private const string KernelInterfacePrefix = "com.apple.kpi.";
    private const string SystemVolumePrefix    = "/System/";

    private static readonly string[] ExtensionRoots = ["/System/Library/Extensions", "/Library/Extensions"];
    private static readonly string[] InfoKeys       = ["CFBundleIdentifier", "CFBundleName", "CFBundleVersion", "OSBundleRequired"];
    private static readonly string[] LoadedInfoKeys = ["CFBundleVersion", "OSBundlePath"];

    private static IEnumerable<string> EnumerateKexts()
    {
        foreach (string root in ExtensionRoots) {
            foreach (string kextPath in AppBundle.EnumerateDirectories(root, "*.kext")) {
                yield return kextPath;

                foreach (string plugInPath in AppBundle.EnumerateDirectories(Path.Combine(kextPath, "Contents", "PlugIns"), "*.kext")) {
                    yield return plugInPath;
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, object?>? FindLoaded(
        IReadOnlyDictionary<string, object?> loadedKexts,
        string                               bundleId,
        string                               bundlePath)
    {
        if (loadedKexts.GetValueOrDefault(bundleId) is not IReadOnlyDictionary<string, object?> loaded) {
            return null;
        }

        return loaded.GetValueOrDefault("OSBundlePath") is not string loadedPath || loadedPath == bundlePath
            ? loaded
            : null;
    }

    internal static List<DriverInfo> GetDrivers()
    {
        Dictionary<string, object?> loadedKexts = IOKit.GetLoadedKextInfo(LoadedInfoKeys) ?? new();

        HashSet<string>  bundlePaths = new(StringComparer.Ordinal);
        List<DriverInfo> drivers     = new();

        foreach (string kextPath in EnumerateKexts()) {
            bundlePaths.Add(kextPath);

            if (MapKext(kextPath, AppBundle.ReadInfo(kextPath, InfoKeys), loadedKexts) is { } driver) {
                drivers.Add(driver);
            }
        }

        foreach ((string bundleId, object? value) in loadedKexts) {
            if (value is not IReadOnlyDictionary<string, object?> loaded ||
                loaded.GetValueOrDefault("OSBundlePath") is not string bundlePath ||
                bundlePaths.Contains(bundlePath)) {
                continue;
            }

            IReadOnlyDictionary<string, object?> info = AppBundle.ReadInfo(bundlePath, InfoKeys)
                ?? new Dictionary<string, object?> { ["CFBundleIdentifier"] = bundleId };

            if (MapKext(bundlePath, info, loadedKexts) is { } driver) {
                drivers.Add(driver);
            }
        }

        foreach (DriverInfo driver in drivers) {
            driver.Publisher = ResolvePublisher(driver.ImagePath!);
        }

        return drivers;
    }

    internal static DriverInfo? MapKext(
        string                                bundlePath,
        IReadOnlyDictionary<string, object?>? info,
        IReadOnlyDictionary<string, object?>  loadedKexts)
    {
        if (info?.GetValueOrDefault("CFBundleIdentifier") is not string { Length: > 0 } bundleId ||
            bundleId.StartsWith(KernelInterfacePrefix, StringComparison.Ordinal)) {
            return null;
        }

        IReadOnlyDictionary<string, object?>? loaded = FindLoaded(loadedKexts, bundleId, bundlePath);

        string? version = loaded?.GetValueOrDefault("CFBundleVersion") as string
            ?? info.GetValueOrDefault("CFBundleVersion") as string;

        string displayName = (info.GetValueOrDefault("CFBundleName") as string)?.Trim().Trim('"').Trim() is { Length: > 0 } name
            ? name
            : bundleId;

        return new DriverInfo {
            Name        = bundleId,
            DisplayName = displayName,
            ImagePath   = bundlePath,
            Version     = version,
            Status      = loaded is null ? DriverStatus.NotLoaded : DriverStatus.Loaded,
            StartType   = MapRequired(info.GetValueOrDefault("OSBundleRequired") as string)
        };
    }

    internal static DriverStartType MapRequired(string? osBundleRequired) => osBundleRequired switch {
        "Root" or "Local-Root" or "Network-Root" => DriverStartType.BootStart,
        "Console" or "Safe Boot"                 => DriverStartType.SystemStart,
                                               _ => DriverStartType.OnDemand
    };

    internal static string? ResolvePublisher(string bundlePath)
    {
        if (CodeSigningPublisher.Parse(CodeSigning.GetSigningCertificateSummary(bundlePath)) is { } publisher) {
            return publisher;
        }

        return bundlePath.StartsWith(SystemVolumePrefix, StringComparison.Ordinal)
            ? CodeSigningPublisher.ApplePublisher
            : null;
    }
}
#endif
