using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public sealed class ProcInfo
{
    public const int MAXCOMLEN = 16;
    public const int MAXPATHLEN = 1024;
    public const int PROC_PIDTASKALLINFO = 2;
    public const int PROC_PIDREGIONPATHINFO = 8;

    public const uint VM_MEMORY_DYLIB = 33;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct proc_bsdinfo
    {
        public uint       pbi_flags;
        public uint       pbi_status;
        public uint       pbi_xstatus;
        public uint       pbi_pid;
        public uint       pbi_ppid;
        public uint       pbi_uid;
        public uint       pbi_gid;
        public uint       pbi_ruid;
        public uint       pbi_rgid;
        public uint       pbi_svuid;
        public uint       pbi_svgid;
        public uint       reserved;
        public fixed byte pbi_comm[MAXCOMLEN];
        public fixed byte pbi_name[MAXCOMLEN * 2];
        public uint       pbi_nfiles;
        public uint       pbi_pgid;
        public uint       pbi_pjobc;
        public uint       e_tdev;
        public uint       e_tpgid;
        public int        pbi_nice;
        public ulong      pbi_start_tvsec;
        public ulong      pbi_start_tvusec;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct proc_taskinfo
    {
        public ulong   pti_virtual_size;
        public ulong   pti_resident_size;
        public ulong   pti_total_user;
        public ulong   pti_total_system;
        public ulong   pti_threads_user;
        public ulong   pti_threads_system;
        public int     pti_policy;
        public int     pti_faults;
        public int     pti_pageins;
        public int     pti_cow_faults;
        public int     pti_messages_sent;
        public int     pti_messages_received;
        public int     pti_syscalls_mach;
        public int     pti_syscalls_unix;
        public int     pti_csw;
        public int     pti_threadnum;
        public int     pti_numrunning;
        public int     pti_priority;
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct proc_taskallinfo
    {
        public proc_bsdinfo    pbsd;
        public proc_taskinfo   ptinfo;
    }

    [DllImport(Libraries.LibProc, SetLastError = true)]
    public static extern unsafe int proc_pidinfo(
        int pid,
        int flavor,
        ulong arg,
        proc_taskallinfo* buffer,
        int bufferSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct proc_regioninfo
    {
        public uint  pri_protection;
        public uint  pri_max_protection;
        public uint  pri_inheritance;
        public uint  pri_flags;
        public ulong pri_offset;
        public uint  pri_behavior;
        public uint  pri_user_wired_count;
        public uint  pri_user_tag;
        public uint  pri_pages_resident;
        public uint  pri_pages_shared_now_private;
        public uint  pri_pages_swapped_out;
        public uint  pri_pages_dirtied;
        public uint  pri_ref_count;
        public uint  pri_shadow_depth;
        public uint  pri_share_mode;
        public uint  pri_private_pages_resident;
        public uint  pri_shared_pages_resident;
        public uint  pri_obj_id;
        public uint  pri_depth;
        public ulong pri_address;
        public ulong pri_size;
    }

    // The embedded vnode_info block (struct vnode_info from <sys/proc_info.h>) is
    // opaque here: we only need the region info and the trailing path, so it is a
    // fixed-size blob that keeps prp_vip_path at the correct offset.
    private const int VnodeInfoSize = 152;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct proc_regionwithpathinfo
    {
        public proc_regioninfo prp_prinfo;
        public fixed byte      prp_vnode_info[VnodeInfoSize];
        public fixed byte      prp_vip_path[MAXPATHLEN];
    }

    [DllImport(Libraries.LibProc, SetLastError = true)]
    public static extern unsafe int proc_pidinfo(
        int pid,
        int flavor,
        ulong arg,
        proc_regionwithpathinfo* buffer,
        int bufferSize);
}