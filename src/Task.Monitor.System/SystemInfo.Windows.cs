using System.Security.Principal;

#if __WIN32__
using Microsoft.Win32;
#endif

namespace Task.Monitor.System;

#pragma warning disable CA1416 // Validate platform compatibility

public static partial class SystemInfo
{
#if __WIN32__
    private static bool GetCpuHighCoreUsageInternal(double processUsagePercent)
    {
        double cpuCoreMaxPercent = (100.0 / (double)Environment.ProcessorCount) / 100.0;
        return (processUsagePercent >= cpuCoreMaxPercent);
    }

    private static string GetOsVersionInternal()
    {
        // Windows product/display name determination is a real mess.
        const string RegPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        const string WINDOWS_10  = "Windows 10";
        const string WINDOWS_11  = "Windows 11";
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegPath);

        if (key == null) {
            return Environment.OSVersion.VersionString;
        }

        string productName = key.GetValue("ProductName") as string ?? string.Empty;
        string displayVersion = key.GetValue("DisplayVersion") as string ?? string.Empty;
        string currBuildNumber = key.GetValue("CurrentBuildNumber") as string ?? string.Empty;

        if (!string.IsNullOrEmpty(productName)) {
            if (int.TryParse(currBuildNumber, out int buildNumber) && buildNumber >= 22000) {
                // Fix up issue where Win 10 -> Win 11 upgrade does not update ProductName in Registry.
                if (productName.Contains(WINDOWS_10, StringComparison.OrdinalIgnoreCase)) {
                    productName = productName.Replace(WINDOWS_10, WINDOWS_11, StringComparison.OrdinalIgnoreCase);
                }
            }

            return string.IsNullOrWhiteSpace(displayVersion)
                ? productName
                : $"{productName} {displayVersion}";
        }

        // For older versions of Windows.
        return Environment.OSVersion.VersionString;
    }

    private static bool IsRunningAsRootInternal()
    {
        using var identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
#endif
}
#pragma warning restore CA1416 // Validate platform compatibility
