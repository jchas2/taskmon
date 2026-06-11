using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public sealed class ProcInfo
{
    public const int MAXCOMLEN = 16;
    public const int PROC_PIDTASKALLINFO = 2;

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
}