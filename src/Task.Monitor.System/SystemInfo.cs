namespace Task.Monitor.System;

public static partial class SystemInfo
{
    public static bool GetCpuHighCoreUsage(double processUsagePercent) => GetCpuHighCoreUsageInternal(processUsagePercent);

    public static string GetOsVersion() => GetOsVersionInternal();

    public static bool IsRunningAsRoot() => IsRunningAsRootInternal();
}
