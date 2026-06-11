using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// Following declarations are found in the Mach Kernel header file mach_host.h

public sealed class LibProc
{
    [DllImport(Libraries.LibProc, SetLastError = true)]
    public static extern unsafe int proc_pid_rusage(
        int pid,
        int flavor,
        SysResource.rusage_info_v3* buffer);

    [DllImport(Libraries.LibProcDyLib, SetLastError = true)]
    public static extern unsafe int proc_listallpids(int* buffer, int buffersize);
    
    [DllImport(Libraries.LibProcDyLib, SetLastError = true)]
    public static extern unsafe int proc_pidpath(
        int pid, 
        byte* buffer, 
        uint buffersize);
}