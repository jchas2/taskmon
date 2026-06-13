using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Task.Monitor.System;

public static partial class SystemInfo
{
    public static bool GetCpuHighCoreUsage(double processUsagePercent) => GetCpuHighCoreUsageInternal(processUsagePercent); 

    public static bool GetCpuTimes(ref SystemTimes systemTimes) => GetCpuTimesInternal(ref systemTimes);
    
    private static IEnumerable<IPAddress> GetIpAddresses(NetworkInterfaceType networkInterfaceType)
    {
        List<NetworkInterface> activeNics = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType == networkInterfaceType)
            .ToList();

        foreach (NetworkInterface nic in activeNics) {
            foreach (UnicastIPAddressInformation ipInfo in nic.GetIPProperties().UnicastAddresses) {
                if (ipInfo.Address.AddressFamily == AddressFamily.InterNetwork) {
                    yield return ipInfo.Address;
                }
            }
        }
    }

    public static bool GetGpuMemory(ref SystemStatistics systemStatistics) => GetGpuMemoryInternal(ref systemStatistics);

    public static bool GetGpuUsage(ref SystemStatistics systemStatistics) => GetGpuUsageInternal(ref systemStatistics);

    public static bool GetGpuCores(ref SystemStatistics systemStatistics) => GetGpuCoresInternal(ref systemStatistics);

    public static bool GetNetworkStats(ref NetworkStatistics networkStatistics) => GetNetworkStatsInternal(ref networkStatistics);
    
    private static IPAddress? GetPreferredIpAddress()
    {
        List<IPAddress> ipAddresses = GetIpAddresses(NetworkInterfaceType.Ethernet).ToList();
        
        if (ipAddresses.Any()) {
            return ipAddresses.First();
        }

        ipAddresses = GetIpAddresses(NetworkInterfaceType.Wireless80211).ToList();
        
        if (ipAddresses.Any()) {
            return ipAddresses.First();
        }

        return null;
    }

    public static bool GetSystemMemory(ref SystemStatistics systemStatistics) => GetSystemMemoryInternal(ref systemStatistics);

    public static bool GetSystemInfo(ref SystemStatistics systemStatistics)
    {
        systemStatistics.MachineName = Environment.MachineName.ToUpper();
        systemStatistics.CpuCores = (ulong)Environment.ProcessorCount;
        
        bool result = GetCpuInfoInternal(ref systemStatistics);

        // GPU core count relies on a GPU being exposed via the platform's device registry, 
        // which is not the case in headless/virtualized environments (e.g. CI
        // runners). A failure here leaves GpuCores at 0 but must not fail the whole gather.
        GetGpuCoresInternal(ref systemStatistics);

        IPAddress? ip = GetPreferredIpAddress();
        
        // With no Nic in an operational status the ip returned can be null.
        systemStatistics.PrivateIPv4Address = ip == null 
            ? string.Empty 
            : ip.ToString();
        
        systemStatistics.OsVersion = GetOsVersionInternal();
        
        return result;
    }
    
    public static bool IsRunningAsRoot() => IsRunningAsRootInternal();
}
