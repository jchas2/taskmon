using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Task.Monitor.Interop.Win32;

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
    
    private static bool GetCpuInfoInternal(ref SystemStatistics systemStatistics)
    {
        const string RegPath = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0\";
        const string RegKeyProcessorName = "ProcessorNameString";
        const string RegKeyFrequencyMhz = "~Mhz";
        
        systemStatistics.CpuCores = (ulong)Environment.ProcessorCount;
        systemStatistics.CpuFrequency = 0;
        systemStatistics.CpuName = string.Empty;

        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegPath);
        Debug.Assert(key != null, $"Failed OpenSubKey() {RegPath}");
        
        if (key == null) {
            return false;
        }
        
        // REG_SZ
        object? processorName = key.GetValue(RegKeyProcessorName);
        
        if (processorName == null) {
            return false;
        }
        
        systemStatistics.CpuName = processorName.ToString() ?? string.Empty;    

        // Reg DWORD
        object? frequency = key.GetValue(RegKeyFrequencyMhz);
        
        if (frequency == null) {
            return false;
        }

        if (!int.TryParse(frequency.ToString() ?? "0", out int frequencyInt32)) {
            return false;
        }
        
        systemStatistics.CpuFrequency = frequencyInt32;
        return true;
    }
    
    private static unsafe bool GetCpuTimesInternal(ref SystemTimes systemTimes)
    {
        MinWinBase.FILETIME lpIdleFileTime;
        MinWinBase.FILETIME lpKernelFileTime;
        MinWinBase.FILETIME lpUserFileTime;

        if (!ProcessThreadsApi.GetSystemTimes( 
            &lpIdleFileTime,
            &lpKernelFileTime,
            &lpUserFileTime)) {
            PInvokeErrorHelpers.AssertOnLastError(nameof(ProcessThreadsApi.GetSystemTimes));
            return false;
        }

        systemTimes.Idle = lpIdleFileTime.ToLong();
        // On Windows, Kernel time also includes Idle time.
        systemTimes.Kernel = lpKernelFileTime.ToLong() - systemTimes.Idle;
        systemTimes.User = lpUserFileTime.ToLong();
        
        return true;
	}

    private static bool GetGpuMemoryInternal(ref SystemStatistics systemStatistics)
    {
        bool result = true;

        if (systemStatistics.TotalGpuMemory == 0) {
            result = GetGpuMemoryInternalTotal(ref systemStatistics);
        }
        
        systemStatistics.AvailableGpuMemory = 0;
        return result && GetGpuMemoryInternalUsed(ref systemStatistics);
    }

    private static bool GetGpuUsageInternal(ref SystemStatistics systemStatistics)
    {
        // Windows derives the system GPU total by summing per-process usage in Processor.
        // No-op here so that summation is left untouched.
        return true;
    }

    private static bool GetGpuCoresInternal(ref SystemStatistics systemStatistics)
    {
        // GPU core count has no portable Windows equivalent; left unreported.
        systemStatistics.GpuCores = 0;
        return true;
    }

    private static bool GetGpuMemoryInternalTotal(ref SystemStatistics systemStatistics)
    {
        // NVIDIA + AMD GPUs store memory info in the display adapter key.
        const string RegPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        const string RegKeyMemorySize = "HardwareInformation.qwMemorySize";
        
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegPath);
        
        if (key == null) {
            return false;
        }                

        // Subkey for each adapter (0000, 0001, 0002 etc).
        foreach (var subKeyName in key.GetSubKeyNames())
        {
            if (!subKeyName.StartsWith("0")) {
                continue;
            }

            using RegistryKey? subKey = key.OpenSubKey(subKeyName);
            object? memorySize = subKey?.GetValue(RegKeyMemorySize);

            if (memorySize == null) {
                continue;
            }
            
            // REG_QWORD.
            if (memorySize is long memLong && memLong > 0) {
                systemStatistics.TotalGpuMemory += memLong;
            }
            else if (memorySize is ulong memULong && memULong > 0) {
                systemStatistics.TotalGpuMemory += (long)memULong;
            }
            else if (memorySize is byte[] bytes && bytes.Length == 8) {
                long memory = BitConverter.ToInt64(bytes, 0);
                systemStatistics.TotalGpuMemory += memory;
            }
        }
        
        return true;
    }
    
    private static bool GetGpuMemoryInternalUsed(ref SystemStatistics systemStatistics)
    {
        const string AdapterMemoryCounterPath = @"\GPU Adapter Memory(*)\Dedicated Usage";
        
        IntPtr hQuery;
        IntPtr hCounter;

        if (Pdh.PdhOpenQuery(
            null,
            IntPtr.Zero,
            out hQuery) != Pdh.ERROR_SUCCESS) {

            return false;
        }

        Pdh.PdhAddEnglishCounter(
            hQuery, 
            AdapterMemoryCounterPath, 
            IntPtr.Zero, 
            out hCounter);
        
        Pdh.PdhCollectQueryData(hQuery);
        long usedDedicatedMemory = SumRawCounterArray(hCounter);
        systemStatistics.AvailableGpuMemory = systemStatistics.TotalGpuMemory - usedDedicatedMemory;

        Pdh.PdhCloseQuery(hQuery);
        return true;        
    }

    private static bool GetNetworkStatsInternal(ref NetworkStatistics networkStatistics)
    {
        networkStatistics.NetworkBytesSent = 0;
        networkStatistics.NetworkBytesReceived = 0;
        networkStatistics.NetworkPacketsSent = 0;
        networkStatistics.NetworkPacketsReceived = 0;
        
        const string BytesReceivedPath = @"\Network Interface(*)\Bytes Received/sec";
        const string BytesSentPath     = @"\Network Interface(*)\Bytes Sent/sec";

        IntPtr hQuery;
        IntPtr hCounterReceived;
        IntPtr hCounterSent;

        if (Pdh.PdhOpenQuery(
            null,
            IntPtr.Zero,
            out hQuery) != Pdh.ERROR_SUCCESS) {

            return false;
        }

        Pdh.PdhAddEnglishCounter(
            hQuery, 
            BytesReceivedPath, 
            IntPtr.Zero, 
            out hCounterReceived);
        
        Pdh.PdhAddEnglishCounter(
            hQuery, 
            BytesSentPath,     
            IntPtr.Zero, 
            out hCounterSent);

        Pdh.PdhCollectQueryData(hQuery);

        long totalReceived = SumRawCounterArray(hCounterReceived);
        long totalSent = SumRawCounterArray(hCounterSent);

        networkStatistics.NetworkBytesReceived = (ulong)totalReceived;
        networkStatistics.NetworkBytesSent = (ulong)totalSent;

        Pdh.PdhCloseQuery(hQuery);
        return true;
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

    private static unsafe bool GetSystemMemoryInternal(ref SystemStatistics systemStatistics)
    {
        systemStatistics.AvailablePageFile = 0;
        systemStatistics.AvailablePhysical = 0;
        systemStatistics.AvailableVirtual = 0;
        systemStatistics.TotalPageFile = 0;
        systemStatistics.TotalPhysical = 0;
        systemStatistics.TotalVirtual = 0;

        SysInfoApi.MEMORYSTATUSEX memoryStatus = new();
        
        if (!SysInfoApi.GlobalMemoryStatusEx(&memoryStatus)) {
            PInvokeErrorHelpers.AssertOnLastError(nameof(SysInfoApi.GlobalMemoryStatusEx));
            return false;
        }

        systemStatistics.AvailablePageFile = memoryStatus.ullAvailPageFile;
        systemStatistics.AvailablePhysical = memoryStatus.ullAvailPhys;
        systemStatistics.AvailableVirtual = memoryStatus.ullAvailVirtual;
        systemStatistics.TotalPageFile = memoryStatus.ullTotalPageFile;
        systemStatistics.TotalPhysical = memoryStatus.ullTotalPhys;
        systemStatistics.TotalVirtual = memoryStatus.ullTotalVirtual;

        return true;
    }
    
    private static bool IsRunningAsRootInternal()
    {
        using var identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    
    private static long SumRawCounterArray(IntPtr hCounter)
    {
        uint bufferSize = 0;
        uint itemCount  = 0;

        Pdh.PdhGetRawCounterArray(
            hCounter, 
            ref bufferSize, 
            ref itemCount, 
            IntPtr.Zero);

        if (bufferSize <= 0) {
            return -1;
        }

        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

        if (Pdh.PdhGetRawCounterArray(
                hCounter,
                ref bufferSize,
                ref itemCount,
                buffer) != Pdh.ERROR_SUCCESS) { 
            
            return 0;
        }

        long total = 0;
        int structSize = Marshal.SizeOf<Pdh.PDH_RAW_COUNTER_ITEM>();

        for (int i = 0; i < itemCount; i++)
        {
            IntPtr ptr  = new IntPtr(buffer.ToInt64() + (i * structSize));
            Pdh.PDH_RAW_COUNTER_ITEM item = Marshal.PtrToStructure<Pdh.PDH_RAW_COUNTER_ITEM>(ptr);

            if (item.RawValue.CStatus == Pdh.PDH_CSTATUS_VALID_DATA) {
                total += item.RawValue.FirstValue;
            }
        }

        Marshal.FreeHGlobal(buffer);
        return total;
    }
#endif
}
#pragma warning restore CA1416 // Validate platform compatibility
