#if __APPLE__
using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.MacOS;

// Application bundles (.app directories), shared by the Startup and InstalledApps scans.
public static class AppBundle
{
    private static readonly string[] BundleInfoKeys = ["CFBundleIdentifier", "CFBundleExecutable"];

    private static readonly string[] PreferredLanguages =
        [.. new[] { CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", "English", "Base" }.Distinct()];

    public static IEnumerable<string> EnumerateApplications(IEnumerable<string> roots)
    {
        foreach (string root in roots) {
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

    public static string[] EnumerateDirectories(string folderPath, string pattern)
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

    public static (string? BundleId, string? Executable) ReadBundleInfo(string appPath)
    {
        if (ReadInfo(appPath, BundleInfoKeys) is not { } info) {
            return (null, null);
        }

        string? bundleId = info.GetValueOrDefault("CFBundleIdentifier") as string;

        string? executable = info.GetValueOrDefault("CFBundleExecutable") is string { Length: > 0 } name
            ? Path.Combine(appPath, "Contents", "MacOS", name)
            : null;

        return (string.IsNullOrEmpty(bundleId) ? null : bundleId, executable);
    }

    public static IReadOnlyDictionary<string, object?>? ReadInfo(string appPath, IReadOnlyList<string> keys) =>
        PropertyList.ReadStrings(Path.Combine(appPath, "Contents", "Info.plist"), keys);

    public static IReadOnlyDictionary<string, object?>? ReadLocalizedInfo(string appPath, IReadOnlyList<string> keys)
    {
        string resources = Path.Combine(appPath, "Contents", "Resources");

        if (PropertyList.ReadStrings(Path.Combine(resources, "InfoPlist.loctable"), keys, PreferredLanguages) is { } table) {
            return table;
        }

        foreach (string language in PreferredLanguages) {
            if (PropertyList.ReadStrings(Path.Combine(resources, $"{language}.lproj", "InfoPlist.strings"), keys) is { } strings) {
                return strings;
            }
        }

        return null;
    }
}
#endif
