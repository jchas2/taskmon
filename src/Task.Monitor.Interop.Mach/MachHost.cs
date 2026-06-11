using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public sealed class MachHost
{
    // Const to set the host_statistics64 flavor arg.
    public const int HOST_CPU_LOAD_INFO = 3;
    
    // Const for host_processor_info.
    public const int PROCESSOR_CPU_LOAD_INFO = 2;

    // Consts used to index into HostCpuLoadInfo::cpu_ticks.
    public const int CPU_STATE_USER = 0;
    public const int CPU_STATE_SYSTEM = 1;
    public const int CPU_STATE_IDLE = 2;
    public const int CPU_STATE_NICE = 3;
    public const int CPU_STATE_MAX = 4;
    
    // Const to set the host_statistics64 flavor arg.
    public const int HOST_VM_INFO64 = 4;

    [DllImport(Libraries.LibSystemDyLib, SetLastError = true)]
    public static extern IntPtr host_self();

    [DllImport(Libraries.LibSystemDyLib, SetLastError = true)]
    public static extern IntPtr mach_host_self();
    
    [DllImport(Libraries.LibSystemDyLib)]
    public static extern int host_processor_info(
        IntPtr host,
        int flavor,
        out uint processorCount,
        out IntPtr processorInfo,
        out uint processorInfoCount);

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern int vm_deallocate(
        IntPtr task,
        IntPtr address,
        IntPtr size);

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern IntPtr mach_task_self();
    
    [StructLayout(LayoutKind.Sequential)]
    public struct HostCpuLoadInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public ulong[] cpu_ticks;
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct VmStatistics64
    {
        public uint  free_count;
        public uint  active_count;
        public uint  inactive_count;
        public uint  wire_count;
        public ulong zero_fill_count;
        public ulong reactivations;
        public ulong pageins;
        public ulong pageouts;
        public ulong faults;
        public ulong cow_faults;
        public ulong lookups;
        public ulong hits;
        public ulong purges;
        public uint  purgeable_count;
        public uint  speculative_count;
        public ulong decompressions;
        public ulong compressions;
        public ulong swapins;
        public ulong swapouts;
        public uint	 compressor_page_count;	
        public uint	 throttled_count;	
        public uint	 external_page_count;	
        public uint	 internal_page_count;	
        public ulong total_uncompressed_pages_in_compressor; 
    }

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern int host_statistics64(
        IntPtr host, 
        int flavor, 
        IntPtr hostInfo, 
        ref int hostInfoCount);
}
