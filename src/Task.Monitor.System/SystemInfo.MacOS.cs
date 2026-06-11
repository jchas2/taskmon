using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System;

public static partial class SystemInfo
{
#if __APPLE__
    private const string kIOServicePlane = "IOService";
    private const uint kIORegistryIterateRecursively = 0x00000001;
    
    private const int NanosecondsTo100NanosecondsFactor = 100;
    
    internal static unsafe TimeSpan CalculateSystemTime(ulong systemTime)
    {
        MachTime.mach_timebase_info_data_t timeBase = default;
        int result = MachTime.mach_timebase_info(&timeBase);
        
        Debug.Assert(result == 0, $"Failed mach_timebase_info(): {result}");
        
        if (result != 0) {
            timeBase.denom = 1;
            timeBase.numer = 1;
        }
        
        return new TimeSpan(
            (Convert.ToInt64(systemTime / NanosecondsTo100NanosecondsFactor * timeBase.numer / timeBase.denom)));
    }
    
    private static bool GetCpuHighCoreUsageInternal(double processUsagePercent) =>
        processUsagePercent >= 100.0;

    private static unsafe bool GetCpuInfoInternal(ref SystemStatistics systemStatistics)
    {
        systemStatistics.CpuCores = (ulong)Environment.ProcessorCount;
        systemStatistics.CpuFrequency = 0;
        systemStatistics.CpuName = string.Empty;

        string? brand = Sys.SysctlByNameString("machdep.cpu.brand_string");

        if (string.IsNullOrEmpty(brand)) {
            return false;
        }

        systemStatistics.CpuName = brand;

        GetPerfLevelCores(ref systemStatistics);
        GetCpuFrequencyInternal(ref systemStatistics);

        return true;
    }

    private static void GetCpuFrequencyInternal(ref SystemStatistics systemStatistics)
    {
        systemStatistics.CpuFrequency = 0;
        systemStatistics.CpuPerformanceFrequency = 0;
        systemStatistics.CpuEfficiencyFrequency = 0;
        systemStatistics.CpuSuperFrequency = 0;

        uint pmgr = FindPmgrEntry();

        if (pmgr == 0) {
            return;
        }

        if (IOKit.IORegistryEntryCreateCFProperties(
            pmgr, 
            out IntPtr properties, 
            IntPtr.Zero, 
            0) != 0 ||
            properties == IntPtr.Zero) {
            return;
        }

        Dictionary<string, nint> props = CoreFoundation.ToDictionary(properties);
        systemStatistics.CpuPerformanceFrequency = MaxDvfsFrequencyMhz(props, "voltage-states5-sram");
        systemStatistics.CpuEfficiencyFrequency = MaxDvfsFrequencyMhz(props, "voltage-states1-sram");
        
        if (systemStatistics.CpuEfficiencyFrequency == 0) {
            systemStatistics.CpuEfficiencyFrequency = MaxDvfsFrequencyMhz(props, "voltage-states9-sram");
        }

        systemStatistics.CpuSuperFrequency = MaxDvfsFrequencyMhz(props, "voltage-states22-sram");
        
        if (systemStatistics.CpuSuperFrequency == 0) {
            systemStatistics.CpuSuperFrequency = MaxDvfsFrequencyMhz(props, "voltage-states23-sram");
        }

        systemStatistics.CpuFrequency = Math.Max(
            systemStatistics.CpuPerformanceFrequency,
            Math.Max(systemStatistics.CpuEfficiencyFrequency, systemStatistics.CpuSuperFrequency));
        
        CoreFoundation.CFRelease(properties);
        IOKit.IOObjectRelease(pmgr);
    }

    private static uint FindPmgrEntry()
    {
        IntPtr matching = IOKit.IOServiceMatching("AppleARMIODevice");

        if (IOKit.IOServiceGetMatchingServices(0, matching, out IntPtr iterator) != 0) {
            return 0;
        }

        uint found = 0;
        byte[] name = new byte[128];
        uint entry;

        while ((entry = IOKit.IOIteratorNext(iterator)) != 0) {
            if (IOKit.IORegistryEntryGetName(entry, name) == 0) {
                int len = Array.IndexOf(name, (byte)0);
                
                len = len < 0 
                    ? name.Length 
                    : len;

                if (Encoding.ASCII.GetString(name, 0, len) == "pmgr") {
                    found = entry;
                    break;
                }
            }

            IOKit.IOObjectRelease(entry);
        }

        IOKit.IOObjectRelease(iterator);
        return found;
    }

    private static double MaxDvfsFrequencyMhz(Dictionary<string, nint> props, string key)
    {
        // A DVFS table is an array of 8-byte entries whose first uint32 is the frequency. M1-M4
        // encode it in Hz, M5+ in kHz; the largest entry is the cluster's max clock (in MHz).
        if (!props.TryGetValue(key, out nint data) || data == IntPtr.Zero) {
            return 0;
        }

        long length = CoreFoundation.CFDataGetLength(data);
        IntPtr bytes = CoreFoundation.CFDataGetBytePtr(data);

        if (length < 8 || bytes == IntPtr.Zero) {
            return 0;
        }

        double maxMhz = 0;

        for (long offset = 0; offset + 8 <= length; offset += 8) {
            uint freq = unchecked((uint)Marshal.ReadInt32(bytes, (int)offset));
            double mhz;

            if (freq >= 100_000_000) {
                mhz = freq / 1_000_000.0;   // Hz -> MHz (M1-M4)
            }
            else if (freq >= 100_000) {
                mhz = freq / 1_000.0;       // kHz -> MHz (M5+)
            }
            else {
                continue;
            }

            if (mhz > maxMhz) {
                maxMhz = mhz;
            }
        }

        return maxMhz;
    }

    private static void GetPerfLevelCores(ref SystemStatistics systemStatistics)
    {
        systemStatistics.CpuPerformanceCores = 0;
        systemStatistics.CpuEfficiencyCores = 0;
        systemStatistics.CpuSuperCores = 0;

        if (!Sys.SysctlByNameInt("hw.nperflevels", out int nperflevels) || nperflevels <= 0) {
            GetPerfLevelCoresLegacy(ref systemStatistics);
            return;
        }

        for (int i = 0; i < nperflevels; i++) {
            string? name = Sys.SysctlByNameString($"hw.perflevel{i}.name");

            if (string.IsNullOrEmpty(name) || !Sys.SysctlByNameInt($"hw.perflevel{i}.logicalcpu", out int count)) {
                continue;
            }

            if (name.StartsWith("Super", StringComparison.Ordinal)) {
                systemStatistics.CpuSuperCores += (ulong)count;
            }
            else if (name.StartsWith("Efficiency", StringComparison.Ordinal)) {
                systemStatistics.CpuEfficiencyCores += (ulong)count;
            }
            else {
                systemStatistics.CpuPerformanceCores += (ulong)count;
            }
        }
    }

    private static void GetPerfLevelCoresLegacy(ref SystemStatistics systemStatistics)
    {
        if (Sys.SysctlByNameInt("hw.perflevel0.logicalcpu", out int pCount)) {
            systemStatistics.CpuPerformanceCores = (ulong)pCount;
        }

        if (Sys.SysctlByNameInt("hw.perflevel1.logicalcpu", out int eCount)) {
            systemStatistics.CpuEfficiencyCores = (ulong)eCount;
        }
    }

    private static bool GetCpuTimesInternal(ref SystemTimes systemTimes)
    {
        uint numCpus = 0;
        uint numCpuInfo = 0;
        IntPtr cpuInfo = IntPtr.Zero;

        if (0 != MachHost.host_processor_info(
            MachHost.mach_host_self(),
            MachHost.PROCESSOR_CPU_LOAD_INFO,
            out numCpus,
            out cpuInfo,
            out numCpuInfo)) {
            
            return false;
        }

        long idleTicks = 0;
        long kernelTicks = 0;
        long userTicks = 0;
        
        int cpuTicksPerCpu = MachHost.CPU_STATE_MAX;
        uint[] ticks = new uint[cpuTicksPerCpu];

        for (int i = 0; i < numCpus; i++) {
            for (int j = 0; j < cpuTicksPerCpu; j++) {
                IntPtr tickPtr = IntPtr.Add(cpuInfo, (i * cpuTicksPerCpu + j) * sizeof(uint));
                ticks[j] = (uint)Marshal.ReadInt32(tickPtr);
            }

            userTicks += ticks[MachHost.CPU_STATE_USER] + ticks[MachHost.CPU_STATE_NICE];
            kernelTicks += ticks[MachHost.CPU_STATE_SYSTEM];
            idleTicks += ticks[MachHost.CPU_STATE_IDLE];
        }

        systemTimes.Idle = idleTicks;
        systemTimes.Kernel = kernelTicks;
        systemTimes.User = userTicks;

        IntPtr size = new IntPtr((int)numCpuInfo * sizeof(int));
        MachHost.vm_deallocate(MachHost.mach_task_self(), cpuInfo, size);

        return true;
    }
    
    private static bool GetGpuCoresInternal(ref SystemStatistics systemStatistics)
    {
        systemStatistics.GpuCores = 0;

        IntPtr matching = IOKit.IOServiceMatching("AGXAccelerator");
        uint service = IOKit.IOServiceGetMatchingService(0, matching);

        if (service == 0) {
            return false;
        }

        IntPtr key = CoreFoundation.CFStringCreate("gpu-core-count");

        IntPtr coreCountRef = IOKit.IORegistryEntrySearchCFProperty(
            service,
            kIOServicePlane,
            key,
            IntPtr.Zero,
            kIORegistryIterateRecursively);

        CoreFoundation.CFRelease(key);

        bool found = false;

        if (coreCountRef != IntPtr.Zero) {
            CoreFoundation.CFNumberGetValue(coreCountRef, out long coreCount);
            systemStatistics.GpuCores = (int)coreCount;
            CoreFoundation.CFRelease(coreCountRef);
            found = true;
        }

        IOKit.IOObjectRelease(service);
        return found;
    }

    private static bool GetGpuMemoryInternal(ref SystemStatistics systemStatistics)
    {
        systemStatistics.TotalGpuMemory = 0;
        systemStatistics.AvailableGpuMemory = 0;

        IntPtr matching = IOKit.IOServiceMatching("IOAccelerator");
        uint accelerator = IOKit.IOServiceGetMatchingService(0, matching);

        if (accelerator == 0) {
            return false;
        }

        IntPtr properties = IntPtr.Zero;

        int result = IOKit.IORegistryEntryCreateCFProperties(
            accelerator,
            out properties,
            IntPtr.Zero,
            0);

        if (result != 0 && properties == IntPtr.Zero) {
            IOKit.IOObjectRelease(accelerator);
            return false;
        }
        
        Dictionary<string, nint> props = CoreFoundation.ToDictionary(properties);

        if (!props.ContainsKey("PerformanceStatistics")) {
            CoreFoundation.CFRelease(properties);
            return false;
        }
        
        Dictionary<string, nint> perfStats = CoreFoundation.ToDictionary(props["PerformanceStatistics"]);
        long allocMemory = 0;
        long inUseMemory = 0;

        if (perfStats.TryGetValue("Alloc system memory", out var propAllocMemory)) {
            CoreFoundation.CFNumberGetValue(propAllocMemory, out allocMemory);
        }

        if (perfStats.TryGetValue("In use system memory", out var propInUseMemory)) {
            CoreFoundation.CFNumberGetValue(propInUseMemory, out inUseMemory);
        }

        systemStatistics.TotalGpuMemory = allocMemory;
        systemStatistics.AvailableGpuMemory = allocMemory - inUseMemory;

        CoreFoundation.CFRelease(properties);
        IOKit.IOObjectRelease(accelerator);

        return true;
    }

    // Persistent IOReport subscription for device-wide GPU performance-state residency.
    private static IntPtr gpuReportSubscription = IntPtr.Zero;
    private static IntPtr gpuReportChannels = IntPtr.Zero;
    private static IntPtr gpuReportPrevSample = IntPtr.Zero;
    private static bool gpuReportInitFailed = false;

    private static bool EnsureGpuReportSubscription()
    {
        if (gpuReportInitFailed) {
            return false;
        }

        if (gpuReportSubscription != IntPtr.Zero) {
            return true;
        }

        IntPtr group = CoreFoundation.CFStringCreate("GPU Stats");
        IntPtr channels = IOReport.IOReportCopyChannelsInGroup(group, IntPtr.Zero, 0, 0, 0);
        CoreFoundation.CFRelease(group);

        if (channels == IntPtr.Zero) {
            gpuReportInitFailed = true;
            return false;
        }

        long count = CoreFoundation.CFDictionaryGetCount(channels);
        IntPtr mutableChannels = CoreFoundation.CFDictionaryCreateMutableCopy(IntPtr.Zero, count, channels);
        CoreFoundation.CFRelease(channels);

        if (mutableChannels == IntPtr.Zero) {
            gpuReportInitFailed = true;
            return false;
        }

        IntPtr subscription = IOReport.IOReportCreateSubscription(
            IntPtr.Zero,
            mutableChannels,
            out _,
            0,
            IntPtr.Zero);

        if (subscription == IntPtr.Zero) {
            CoreFoundation.CFRelease(mutableChannels);
            gpuReportInitFailed = true;
            return false;
        }

        gpuReportChannels = mutableChannels;
        gpuReportSubscription = subscription;
        return true;
    }

    private static bool GetGpuUsageInternal(ref SystemStatistics systemStatistics)
    {
        systemStatistics.GpuPercentTime = 0.0;

        if (!EnsureGpuReportSubscription()) {
            return false;
        }

        IntPtr sample = IOReport.IOReportCreateSamples(gpuReportSubscription, gpuReportChannels, IntPtr.Zero);

        if (sample == IntPtr.Zero) {
            return false;
        }

        if (gpuReportPrevSample != IntPtr.Zero) {
            IntPtr delta = IOReport.IOReportCreateSamplesDelta(gpuReportPrevSample, sample, IntPtr.Zero);

            if (delta != IntPtr.Zero) {
                if (TryGetGpuResidency(delta, out double active)) {
                    systemStatistics.GpuPercentTime = active;
                }

                CoreFoundation.CFRelease(delta);
            }

            CoreFoundation.CFRelease(gpuReportPrevSample);
        }

        gpuReportPrevSample = sample;
        return true;
    }

    private static bool TryGetGpuResidency(IntPtr deltaSample, out double activeFraction)
    {
        activeFraction = 0.0;
        Dictionary<string, nint> top = CoreFoundation.ToDictionary(deltaSample);

        if (!top.TryGetValue("IOReportChannels", out var channelsArray)) {
            return false;
        }

        long channelCount = CoreFoundation.CFArrayGetCount(channelsArray);

        for (long i = 0; i < channelCount; i++) {
            IntPtr item = CoreFoundation.CFArrayGetValueAtIndex(channelsArray, i);

            if (CoreFoundation.GetString(IOReport.IOReportChannelGetGroup(item)) != "GPU Stats") {
                continue;
            }

            if (CoreFoundation.GetString(IOReport.IOReportChannelGetSubGroup(item)) != "GPU Performance States") {
                continue;
            }

            if (CoreFoundation.GetString(IOReport.IOReportChannelGetChannelName(item)) != "GPUPH") {
                continue;
            }

            int stateCount = IOReport.IOReportStateGetCount(item);
            long totalTime = 0;
            long activeTime = 0;

            for (int s = 0; s < stateCount; s++) {
                long residency = IOReport.IOReportStateGetResidency(item, s);
                totalTime += residency;

                string? stateName = CoreFoundation.GetString(IOReport.IOReportStateGetNameForIndex(item, s));

                if (stateName != "OFF" && stateName != "IDLE" && stateName != "DOWN") {
                    activeTime += residency;
                }
            }

            if (totalTime > 0) {
                activeFraction = (double)activeTime / (double)totalTime;
                return true;
            }
        }

        return false;
    }

    private static unsafe bool GetNetworkStatsInternal(ref NetworkStatistics networkStatistics)
    {
        networkStatistics.NetworkBytesSent = 0;
        networkStatistics.NetworkBytesReceived = 0;
        networkStatistics.NetworkPacketsSent = 0;
        networkStatistics.NetworkPacketsReceived = 0;

        ReadOnlySpan<int> sysctlName = [
            (int)Sys.Selectors.CTL_NET,
            Sys.PF_ROUTE,
            0,
            Sys.AF_UNSPEC,
            (int)Sys.NetRouting.NET_RT_IFLIST2,
            0
        ];

        byte* buffer = null;
        int bytesLength = 0;

        if (!Sys.Sysctl(sysctlName, ref buffer, ref bytesLength)) {
            return false;
        }

        if (buffer == null || bytesLength == 0) {
            Sys.FreeMemory(buffer);
            return false;
        }

        byte* current = buffer;
        byte* end = buffer + bytesLength;

        while (current < end)
        {
            // Read the message header.
            if (current + sizeof(ushort) > end) {
                break;
            }

            ushort msgLen = *(ushort*)current;

            if (msgLen == 0 || current + msgLen > end) {
                break;
            }

            // Check if this is an interface info message.
            if (current + 4 <= end) {
                byte msgType = *(current + 3);

                if (msgType == Sys.RTM_IFINFO2) {
                    if (current + Marshal.SizeOf<Sys.if_msghdr2>() <= end) {
                        Sys.if_msghdr2* ifMsg = (Sys.if_msghdr2*)current;
                        networkStatistics.NetworkBytesSent += ifMsg->ifm_data.ifi_obytes;
                        networkStatistics.NetworkBytesReceived += ifMsg->ifm_data.ifi_ibytes;
                        networkStatistics.NetworkPacketsSent += ifMsg->ifm_data.ifi_opackets;
                        networkStatistics.NetworkPacketsReceived += ifMsg->ifm_data.ifi_ipackets;
                    }
                }
            }

            // Move to next message.
            current += msgLen;
        }

        Sys.FreeMemory(buffer);
        return true;
    }
    
    private static unsafe int GetPageSize()
    {
        int pageSize = 0;
        ReadOnlySpan<int> sysctlName = [(int)Sys.Selectors.CTL_HW, (int)Sys.Hardware.HW_PAGESIZE];

        byte* buffer = null;
        int bytesLength = 0;

        if (!Sys.Sysctl(
            sysctlName, 
            ref buffer, 
            ref bytesLength)) {
            
            Sys.FreeMemory(buffer);
            return 0;
        }

        if (bytesLength == sizeof(int)) {
            pageSize = *(int*)buffer;
        }
        
        Sys.FreeMemory(buffer);

        return pageSize;
    }
    
    private static unsafe bool GetSystemMemoryInternal(ref SystemStatistics systemStatistics)
    {
        systemStatistics.AvailablePageFile = 0;
        systemStatistics.AvailablePhysical = 0;
        systemStatistics.AvailableVirtual = 0;
        systemStatistics.TotalPageFile = 0;
        systemStatistics.TotalPhysical = 0;
        systemStatistics.TotalVirtual = 0;
        
        IntPtr host = MachHost.host_self();
        int count = (int)(Marshal.SizeOf<MachHost.VmStatistics64>() / sizeof(int));
        IntPtr vmStatisticsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MachHost.VmStatistics64>());
        
        if (0 != MachHost.host_statistics64(
            host,
            MachHost.HOST_VM_INFO64,
            vmStatisticsPtr,
            ref count)) {
            
            Marshal.FreeHGlobal(vmStatisticsPtr);
            return false;
        }

        var info = Marshal.PtrToStructure<MachHost.VmStatistics64>(vmStatisticsPtr);
        int pageSize = GetPageSize();

        ReadOnlySpan<int> sysctlName = [(int)Sys.Selectors.CTL_HW, (int)Sys.Hardware.HW_MEMSIZE];
        byte* buffer = null;
        int bytesLength = 0;

        if (!Sys.Sysctl(
            sysctlName, 
            ref buffer, 
            ref bytesLength) || bytesLength != 8) {

            Sys.FreeMemory(buffer);
            return false;
        }
        
        ulong totalPhysical = *(ulong*)buffer;
        Sys.FreeMemory(buffer);
        
        long totalUsedCount = info.wire_count + info.inactive_count + info.active_count + info.compressor_page_count;
        long totalUsed = totalUsedCount * pageSize;
        
        systemStatistics.TotalPhysical = totalPhysical;
        systemStatistics.AvailablePhysical = totalPhysical - (ulong)totalUsed;

        sysctlName = [(int)Sys.Selectors.CTL_VM, Sys.VM_SWAPUSAGE];
        buffer = null;
        bytesLength = 0;

        if (!Sys.Sysctl(
            sysctlName, 
            ref buffer, 
            ref bytesLength) || bytesLength != 32) {

            Sys.FreeMemory(buffer);
            return false;
        }
        
        Sys.XswUsage* xswUsage = (Sys.XswUsage*)buffer;

        systemStatistics.TotalPageFile = xswUsage->total;
        systemStatistics.AvailablePageFile = xswUsage->avail;

        Sys.FreeMemory(buffer);
        Marshal.FreeHGlobal(vmStatisticsPtr);
        
        return true;
    }

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
