#if __WIN32__
using Microsoft.Win32;
#endif

namespace Task.Monitor.System.Services.InstalledApps;

#pragma warning disable CA1416 // Validate platform compatibility

public partial class InstalledAppsService
{
#if __WIN32__
    private const string UninstallKeyPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private partial InstalledAppsSpecs ScanInstalledApps()
    {
        InstalledAppsSpecs specs = new();

        ScanUninstallKey(specs, RegistryHive.LocalMachine, RegistryView.Registry64, InstalledAppScope.Machine);

        // The 32-bit registry view is a distinct set of keys only on a 64-bit OS; on 32-bit Windows
        // it aliases the same key and would double every entry.
        if (Environment.Is64BitOperatingSystem) {
            ScanUninstallKey(specs, RegistryHive.LocalMachine, RegistryView.Registry32, InstalledAppScope.Machine);
        }

        ScanUninstallKey(specs, RegistryHive.CurrentUser, RegistryView.Default, InstalledAppScope.User);

        specs.Apps.Sort(static (left, right) =>
            string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

        return specs;
    }

    private void ScanUninstallKey(
        InstalledAppsSpecs specs, RegistryHive hive, RegistryView view, InstalledAppScope scope)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? uninstallKey = baseKey.OpenSubKey(UninstallKeyPath);

        if (uninstallKey is null) {
            return;
        }

        string originPrefix = $"{HiveShortName(hive)}\\{UninstallKeyPath}";

        foreach (string subKeyName in uninstallKey.GetSubKeyNames()) {
            using RegistryKey? appKey = uninstallKey.OpenSubKey(subKeyName);

            if (appKey is null) {
                continue;
            }

            InstalledApp? app = InstalledAppRegistryEntry.Parse(
                displayName: appKey.GetValue("DisplayName") as string,
                displayVersion: appKey.GetValue("DisplayVersion") as string,
                publisher: appKey.GetValue("Publisher") as string,
                installDate: appKey.GetValue("InstallDate") as string,
                installLocation: appKey.GetValue("InstallLocation") as string,
                uninstallString: appKey.GetValue("UninstallString") as string,
                quietUninstallString: appKey.GetValue("QuietUninstallString") as string,
                estimatedSizeKb: appKey.GetValue("EstimatedSize") as int?,
                systemComponent: appKey.GetValue("SystemComponent") as int?,
                parentKeyName: appKey.GetValue("ParentKeyName") as string,
                releaseType: appKey.GetValue("ReleaseType") as string,
                scope: scope,
                origin: $"{originPrefix}\\{subKeyName}");

            if (app is not null) {
                specs.Apps.Add(app);
            }
        }
    }

    private static string HiveShortName(RegistryHive hive) => hive switch {
        RegistryHive.LocalMachine => "HKLM",
        RegistryHive.CurrentUser => "HKCU",
        _ => hive.ToString()
    };
#endif
}

#pragma warning restore CA1416 // Validate platform compatibility
