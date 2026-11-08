using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System;

public static partial class SystemInfo
{
#if __APPLE__
    private static bool GetCpuHighCoreUsageInternal(double processUsagePercent) =>
        processUsagePercent >= 100.0;

    private static string GetOsVersionInternal()
    {
        string? version = Sys.SysctlByNameString("kern.osproductversion");

        if (string.IsNullOrEmpty(version)) {
            return Environment.OSVersion.VersionString;
        }

        int dot = version.IndexOf('.');
        _ = int.TryParse(dot < 0 ? version : version.Substring(0, dot), out var major);

        string name = major switch {
            26 => "Tahoe",
            15 => "Sequoia",
            14 => "Sonoma",
            13 => "Ventura",
            12 => "Monterey",
            11 => "Big Sur",
            _  => "macOS"
        };

        return $"{name} {version}";
    }

    private static bool IsRunningAsRootInternal() =>
        UniStd.geteuid() == 0;
#endif
}
