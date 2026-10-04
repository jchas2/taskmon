#if __APPLE__
using System.Runtime.InteropServices;
using System.Text;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Cpu;

public partial class CpuService
{
    private void OnStartCpuSpecs(ref CpuSpecs specs)
    {
        specs.CpuCores = (ulong)Environment.ProcessorCount;
        specs.CpuSockets = 1;
        specs.CpuName = Sys.SysctlByNameString("machdep.cpu.brand_string") ?? string.Empty;

        // CpuVirtualizationFirmwareEnabled is a Windows firmware notion with no macOS analogue.
        specs.CpuVirtualizationFirmwareEnabled = false;

        PopulatePerfLevelCores(ref specs);
        PopulateCpuFrequency(ref specs);
        PopulateCpuCaches(ref specs);
    }

    private static void PopulateCpuCaches(ref CpuSpecs specs)
    {
        // hw.*cachesize sysctls are 64-bit. On Apple Silicon these report the performance cluster;
        // L3 is usually absent and simply stays 0. L1 uses the data cache size.
        if (Sys.SysctlByNameLong("hw.l1dcachesize", out long l1) && l1 > 0) {
            specs.CpuL1CacheBytes = (ulong)l1;
        }

        if (Sys.SysctlByNameLong("hw.l2cachesize", out long l2) && l2 > 0) {
            specs.CpuL2CacheBytes = (ulong)l2;
        }

        if (Sys.SysctlByNameLong("hw.l3cachesize", out long l3) && l3 > 0) {
            specs.CpuL3CacheBytes = (ulong)l3;
        }
    }

    private static void PopulatePerfLevelCores(ref CpuSpecs specs)
    {
        specs.CpuPerformanceCores = 0;
        specs.CpuEfficiencyCores = 0;
        specs.CpuSuperCores = 0;

        if (!Sys.SysctlByNameInt("hw.nperflevels", out int nperflevels) || nperflevels <= 0) {
            if (Sys.SysctlByNameInt("hw.perflevel0.logicalcpu", out int pCount)) {
                specs.CpuPerformanceCores = (ulong)pCount;
            }

            if (Sys.SysctlByNameInt("hw.perflevel1.logicalcpu", out int eCount)) {
                specs.CpuEfficiencyCores = (ulong)eCount;
            }

            return;
        }

        for (int i = 0; i < nperflevels; i++) {
            string? name = Sys.SysctlByNameString($"hw.perflevel{i}.name");

            if (string.IsNullOrEmpty(name) ||
                !Sys.SysctlByNameInt($"hw.perflevel{i}.logicalcpu", out int count)) {
                continue;
            }

            if (name.StartsWith("Super", StringComparison.Ordinal)) {
                specs.CpuSuperCores += (ulong)count;
            }
            else if (name.StartsWith("Efficiency", StringComparison.Ordinal)) {
                specs.CpuEfficiencyCores += (ulong)count;
            }
            else {
                specs.CpuPerformanceCores += (ulong)count;
            }
        }
    }

    private static void PopulateCpuFrequency(ref CpuSpecs specs)
    {
        uint pmgr = FindPmgrEntry();

        if (pmgr == 0) {
            return;
        }

        if (IOKit.IORegistryEntryCreateCFProperties(pmgr, out IntPtr properties, IntPtr.Zero, 0) != 0 ||
            properties == IntPtr.Zero) {

            IOKit.IOObjectRelease(pmgr);
            return;
        }

        Dictionary<string, nint> props = CoreFoundation.ToDictionary(properties);

        specs.CpuPerformanceFrequency = MaxDvfsFrequencyMhz(props, "voltage-states5-sram");
        specs.CpuEfficiencyFrequency = MaxDvfsFrequencyMhz(props, "voltage-states1-sram");

        if (specs.CpuEfficiencyFrequency == 0) {
            specs.CpuEfficiencyFrequency = MaxDvfsFrequencyMhz(props, "voltage-states9-sram");
        }

        specs.CpuSuperFrequency = MaxDvfsFrequencyMhz(props, "voltage-states22-sram");

        if (specs.CpuSuperFrequency == 0) {
            specs.CpuSuperFrequency = MaxDvfsFrequencyMhz(props, "voltage-states23-sram");
        }

        specs.CpuFrequency = Math.Max(
            specs.CpuPerformanceFrequency,
            Math.Max(specs.CpuEfficiencyFrequency, specs.CpuSuperFrequency));

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
                len = len < 0 ? name.Length : len;

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
}
#endif
