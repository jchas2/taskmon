using System.Runtime.InteropServices;
using System.Text;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
#if __APPLE__
    // ri_user_time / ri_system_time arrive in mach time units; mach_timebase_info converts them to
    // nanoseconds, which are then folded into 100ns FILETIME-style ticks so the platform-neutral
    // rate math (ProcessEntryCalculator.FileTimeTicksPerSecond) treats macOS and Windows alike.
    private const int NanosecondsTo100NanosecondsFactor = 100;

    private static MachTime.mach_timebase_info_data_t cachedTimebase;
    private static bool timebaseInitialized;

    private static List<ProcessSample> GetProcessSamples()
    {
        int[] pids = GetPids();
        List<ProcessSample> samples = new(pids.Length);

        for (int i = 0; i < pids.Length; i++) {
            ProcessSample? sample = CreateProcessSample(pids[i]);

            if (sample != null) {
                samples.Add(sample);
            }
        }

        return samples;
    }

    public static ProcessSample? GetProcessSample(int pid) => CreateProcessSample(pid);

    private static unsafe ProcessSample? CreateProcessSample(int pid)
    {
        const int KernelTaskPid = 0;
        const int LaunchdPid    = 1;

        int size = sizeof(ProcInfo.proc_taskallinfo);
        ProcInfo.proc_taskallinfo procTaskInfo = default;

        int result = ProcInfo.proc_pidinfo(
            pid,
            ProcInfo.PROC_PIDTASKALLINFO,
            0,
            &procTaskInfo,
            size);

        if (result != size) {
            return null;
        }

        SysResource.rusage_info_v3 usage = new();
        result = LibProc.proc_pid_rusage(pid, SysResource.RUSAGE_INFO_V3, &usage);

        if (result < 0) {
            return null;
        }

        ProcInfo.proc_bsdinfo procBsdInfo = procTaskInfo.pbsd;
        IntPtr pbiCommPtr = new((void*)procBsdInfo.pbi_comm);
        IntPtr pbiNamePtr = new((void*)procTaskInfo.pbsd.pbi_name);

        ProcessSample sample = new() {
            Pid = pid
        };

        sample.ProcessName     = Marshal.PtrToStringUTF8(pbiCommPtr) ?? string.Empty;
        sample.FileName        = GetProcPidPath(pid);
        sample.FileDescription = Marshal.PtrToStringUTF8(pbiNamePtr) ?? sample.ProcessName;
        sample.ModuleName      = Path.GetFileName(sample.FileName);
        sample.ParentPid       = (int)procBsdInfo.pbi_ppid;
        sample.IsDaemon        = procBsdInfo.pbi_ppid == LaunchdPid || pid == LaunchdPid || pid == KernelTaskPid;
        sample.IsLowPriority   = procBsdInfo.pbi_nice > 0;
        sample.IsRunningAsRoot = procBsdInfo.pbi_uid == 0;
        sample.UserName        = GetProcessUserName(ref procTaskInfo);
        sample.CmdLine         = sample.FileName;
        sample.ThreadCount     = procTaskInfo.ptinfo.pti_threadnum;
        sample.HandleCount     = 0;
        sample.BasePriority    = procBsdInfo.pbi_nice;
        sample.UsedMemory      = (long)procTaskInfo.ptinfo.pti_resident_size;
        sample.KernelTime      = CalculateSystemTime(usage.ri_system_time).Ticks;
        sample.UserTime        = CalculateSystemTime(usage.ri_user_time).Ticks;
        sample.DiskReadBytes   = usage.ri_diskio_bytesread;
        sample.DiskWriteBytes  = usage.ri_diskio_byteswritten;

        return sample;
    }

    private static unsafe int[] GetPids()
    {
        int newSize = LibProc.proc_listallpids(null, 0);

        if (newSize <= 0) {
            return new[] { Environment.ProcessId };
        }

        int[] buffer;

        do {
            buffer = new int[(int)(newSize * 1.1)];

            fixed (int* pBuffer = &buffer[0]) {
                newSize = LibProc.proc_listallpids(pBuffer, buffer.Length * sizeof(int));

                if (newSize <= 0) {
                    return new[] { Environment.ProcessId };
                }
            }
        }
        while (newSize == buffer.Length);

        Array.Resize(ref buffer, newSize);
        return buffer;
    }

    private static unsafe string GetProcessUserName(ref ProcInfo.proc_taskallinfo procTaskInfo)
    {
        uint uid = procTaskInfo.pbsd.pbi_uid;
        const int bufferSize = Pwd.Passwd.InitialBufferSize;
        byte* buf = stackalloc byte[bufferSize];

        int error = Pwd.GetPwUidR(
            uid,
            out Pwd.Passwd passwd,
            buf,
            bufferSize);

        if (error == 0 && passwd.Name != null) {
            return Marshal.PtrToStringUTF8((IntPtr)passwd.Name) ?? string.Empty;
        }

        return string.Empty;
    }

    private static unsafe string GetProcPidPath(int pid)
    {
        const int bufferSize = 4096;
        byte* buffer = stackalloc byte[bufferSize];

        int byteCount = LibProc.proc_pidpath(
            pid,
            buffer,
            bufferSize);

        if (byteCount <= 0) {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(buffer, byteCount);
    }

    private static unsafe TimeSpan CalculateSystemTime(ulong systemTime)
    {
        if (!timebaseInitialized) {
            MachTime.mach_timebase_info_data_t timeBase = default;

            if (MachTime.mach_timebase_info(&timeBase) != 0 || timeBase.denom == 0) {
                timeBase.numer = 1;
                timeBase.denom = 1;
            }

            cachedTimebase = timeBase;
            timebaseInitialized = true;
        }

        return new TimeSpan(
            (long)(systemTime / NanosecondsTo100NanosecondsFactor * cachedTimebase.numer / cachedTimebase.denom));
    }
#endif
}
