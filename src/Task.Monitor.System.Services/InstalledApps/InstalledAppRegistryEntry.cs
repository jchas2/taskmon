using System.Globalization;

namespace Task.Monitor.System.Services.InstalledApps;

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

    private static bool IsUpdateReleaseType(string? releaseType) => releaseType 
        is "Update" 
        or "Security Update" 
        or "Hotfix" 
        or "ServicePack";

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private static DateTime? ParseInstallDate(string? installDate) =>
        !string.IsNullOrEmpty(installDate) &&
        DateTime.TryParseExact(
            installDate, 
            "yyyyMMdd", 
            CultureInfo.InvariantCulture, 
            DateTimeStyles.None, 
            out DateTime parsed)
                ? parsed
                : null;
}
