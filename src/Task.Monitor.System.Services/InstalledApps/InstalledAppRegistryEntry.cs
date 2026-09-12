using System.Globalization;

namespace Task.Monitor.System.Services.InstalledApps;

// Decides whether an Uninstall registry subkey represents a user-facing installed application, and
// normalises its raw values into an InstalledApp. Takes plain values rather than a RegistryKey so
// the filtering rules - the same ones Programs and Features applies to this same key - are testable
// without touching the registry: skip entries with no display name, skip internal components
// (SystemComponent), and skip updates/patches attached to a parent product (ParentKeyName,
// ReleaseType).
public static class InstalledAppRegistryEntry
{
    public static InstalledApp? Parse(
        string? displayName,
        string? displayVersion,
        string? publisher,
        string? installDate,
        string? installLocation,
        string? uninstallString,
        string? quietUninstallString,
        int? estimatedSizeKb,
        int? systemComponent,
        string? parentKeyName,
        string? releaseType,
        InstalledAppScope scope,
        string origin)
    {
        if (string.IsNullOrWhiteSpace(displayName)) {
            return null;
        }

        if (systemComponent == 1) {
            return null;
        }

        if (!string.IsNullOrEmpty(parentKeyName)) {
            return null;
        }

        if (IsUpdateReleaseType(releaseType)) {
            return null;
        }

        return new InstalledApp {
            Name = displayName,
            Version = NullIfEmpty(displayVersion),
            Publisher = NullIfEmpty(publisher),
            InstallDate = ParseInstallDate(installDate),
            InstallLocation = NullIfEmpty(installLocation),
            EstimatedSizeKb = estimatedSizeKb,
            UninstallCommand = NullIfEmpty(quietUninstallString) ?? NullIfEmpty(uninstallString),
            Scope = scope,
            Origin = origin
        };
    }

    private static bool IsUpdateReleaseType(string? releaseType) =>
        releaseType is "Update" or "Security Update" or "Hotfix" or "ServicePack";

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    // Stored as an 8-digit "yyyyMMdd" string when present at all.
    private static DateTime? ParseInstallDate(string? installDate) =>
        !string.IsNullOrEmpty(installDate) &&
        DateTime.TryParseExact(
            installDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
            ? parsed
            : null;
}
