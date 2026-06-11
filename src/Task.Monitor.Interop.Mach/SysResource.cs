using System.Runtime.InteropServices;

// Following declarations are found in the Mach Kernel header file sys/resource.h

namespace Task.Monitor.Interop.Mach;

public sealed class SysResource
{
    public const int RUSAGE_INFO_V3 = 3;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct rusage_info_v3
    {
        public fixed byte     ri_uuid[16];
        public ulong          ri_user_time;
        public ulong          ri_system_time;
        public ulong          ri_pkg_idle_wkups;
        public ulong          ri_interrupt_wkups;
        public ulong          ri_pageins;
        public ulong          ri_wired_size;
        public ulong          ri_resident_size;
        public ulong          ri_phys_footprint;
        public ulong          ri_proc_start_abstime;
        public ulong          ri_proc_exit_abstime;
        public ulong          ri_child_user_time;
        public ulong          ri_child_system_time;
        public ulong          ri_child_pkg_idle_wkups;
        public ulong          ri_child_interrupt_wkups;
        public ulong          ri_child_pageins;
        public ulong          ri_child_elapsed_abstime;
        public ulong          ri_diskio_bytesread;
        public ulong          ri_diskio_byteswritten;
        public ulong          ri_cpu_time_qos_default;
        public ulong          ri_cpu_time_qos_maintenance;
        public ulong          ri_cpu_time_qos_background;
        public ulong          ri_cpu_time_qos_utility;
        public ulong          ri_cpu_time_qos_legacy;
        public ulong          ri_cpu_time_qos_user_initiated;
        public ulong          ri_cpu_time_qos_user_interactive;
        public ulong          ri_billed_system_time;
        public ulong          ri_serviced_system_time;
    }
}