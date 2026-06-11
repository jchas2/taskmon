using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class PsApi
{
    const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_MEMORY_COUNTERS
    {
        public uint  cb;
        public uint  PageFaultCount;
        public nuint PeakWorkingSetSize;
        public nuint WorkingSetSize;
        public nuint QuotaPeakPagedPoolUsage;
        public nuint QuotaPagedPoolUsage;
        public nuint QuotaNonPagedPoolUsage;
        public nuint QuotaPeakNonPagedPoolUsage;
        public nuint PagefileUsage;
        public nuint PeakPagefileUsage;
    }

    [DllImport(Libraries.PsApi, SetLastError = true)]
    public static extern bool GetProcessMemoryInfo(
        IntPtr hProcess,
        ref PROCESS_MEMORY_COUNTERS psmemCounters,
        uint cb);
}