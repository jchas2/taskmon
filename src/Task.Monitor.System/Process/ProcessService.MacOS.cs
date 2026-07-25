using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Process;

public sealed partial class ProcessService
{
#if __APPLE__
    private unsafe ProcessInfo? CreateProcessInfo(int pid, long gpuTime)
    {
        const int KERNEL_TASK_PID = 0;
        const int LAUNCHD_PID = 1;

        int size = sizeof(ProcInfo.proc_taskallinfo);
        ProcInfo.proc_taskallinfo procTaskInfo = default(ProcInfo.proc_taskallinfo);
        
        int result = ProcInfo.proc_pidinfo(
            pid, 
            ProcInfo.PROC_PIDTASKALLINFO, 
            0, 
            &procTaskInfo, 
            size);

        if (result != size) {
            return null;
        }
        
        SysResource.rusage_info_v3 info = new();
        result = LibProc.proc_pid_rusage(pid, SysResource.RUSAGE_INFO_V3, &info);
        
        if (result < 0) {
            return null;
        }

        ProcessInfo processInfo = new() {
            Pid = pid
        };

        ProcInfo.proc_bsdinfo procBsdInfo = procTaskInfo.pbsd;
        IntPtr pbiCommPtr = new((void*)procBsdInfo.pbi_comm);
        IntPtr pbiNamePtr = new((void*)procTaskInfo.pbsd.pbi_name);
        
        processInfo.ParentPid = 0;
        processInfo.ProcessName = Marshal.PtrToStringUTF8(pbiCommPtr) ?? string.Empty;
        processInfo.FileName = GetProcPidPath(pid);
        processInfo.FileDescription = Marshal.PtrToStringUTF8(pbiNamePtr) ?? processInfo.ProcessName;
        processInfo.ModuleName = Path.GetFileName(processInfo.FileName);
        processInfo.IsDaemon = procTaskInfo.pbsd.pbi_ppid == LAUNCHD_PID || pid == LAUNCHD_PID || pid == KERNEL_TASK_PID;
        processInfo.IsLowPriority = procTaskInfo.pbsd.pbi_nice > 0;
        processInfo.IsRunningAsRoot = procBsdInfo.pbi_uid == 0;
        processInfo.UserName = GetProcessUserName(ref procTaskInfo);
        processInfo.CmdLine = processInfo.FileName;
        processInfo.ThreadCount = procTaskInfo.ptinfo.pti_threadnum;
        processInfo.HandleCount = 0;
        processInfo.BasePriority = procTaskInfo.pbsd.pbi_nice;
        processInfo.UsedMemory = (long)procTaskInfo.ptinfo.pti_resident_size;
        processInfo.KernelTime = SystemInfo.CalculateSystemTime(info.ri_system_time).Ticks;
        processInfo.UserTime = SystemInfo.CalculateSystemTime(info.ri_user_time).Ticks;
        processInfo.GpuTime = gpuTime;
        processInfo.DiskReadBytes = info.ri_diskio_bytesread;
        processInfo.DiskWriteBytes = info.ri_diskio_byteswritten;
        processInfo.DiskOperations = info.ri_diskio_bytesread + info.ri_diskio_byteswritten;

        return processInfo;
    }
    
    private unsafe int[] GetPids()
    {
        int newSize = LibProc.proc_listallpids((int*)null, 0);

        if (newSize <= 0) {
            return new int[1] { Environment.ProcessId };
        }
        
        int[] array;
        
        do
        {
            array = new int[(int) ((double)newSize * 1.1)];
            
            fixed (int* pBuffer = &array[0]) {
                newSize = LibProc.proc_listallpids(pBuffer, array.Length * 4);
                
                // TOOD: Don't throw. 
                if (newSize <= 0)
                    throw new InvalidOperationException();
            }
        }
        while (newSize == array.Length);
        
        Array.Resize<int>(ref array, newSize);
        return array;
    }

    private ProcessInfo? GetProcessInfoInternal(int pid)
    {
        Dictionary<int, long> gpuStats = GpuService.GetProcessStats();
        long gpuTime = gpuStats.GetValueOrDefault(pid, 0);
        
        ProcessInfo? pinfo = CreateProcessInfo(pid, gpuTime);
        return pinfo;
    }
    
    private IEnumerable<ProcessInfo> GetProcessInfosInternal()
    {
        int[] pids = GetPids();
        Dictionary<int, long> gpuStats = GpuService.GetProcessStats();

        for (int i = 0; i < pids.Length; i++) {
            long gpuTime = gpuStats.GetValueOrDefault(pids[i], 0);
            ProcessInfo? pinfo = CreateProcessInfo(pids[i], gpuTime);
            
            if (pinfo != null) {
                yield return pinfo;
            }
        }
    }
    
    private static unsafe string GetProcessUserName(ref ProcInfo.proc_taskallinfo procTaskInfo)
    {
        uint uid = procTaskInfo.pbsd.pbi_uid;
        const int bufferSize = Pwd.Passwd.InitialBufferSize;
        byte* buf = stackalloc byte[bufferSize];

        Pwd.Passwd passwd;
        
        int error = Pwd.GetPwUidR(
            uid, 
            out passwd, 
            buf, 
            bufferSize);
        
        if (0 == error && null != passwd.Name) {
            return Marshal.PtrToStringUTF8((IntPtr)passwd.Name) ?? string.Empty;
        }
        
        return string.Empty;
    }

    private unsafe string GetProcPidPath(int pid)
    {
        byte* buffer = stackalloc byte[4096];
        
        int byteCount = LibProc.proc_pidpath(
            pid, 
            buffer, 
            4096U);

        if (byteCount <= 0) {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(buffer, byteCount);
    }
#endif
}