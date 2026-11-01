using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class PsApi
{
    const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    public struct PERFORMANCE_INFORMATION
    {
        public uint  cb;
        public nuint CommitTotal;
        public nuint CommitLimit;
        public nuint CommitPeak;
        public nuint PhysicalTotal;
        public nuint PhysicalAvailable;
        public nuint SystemCache;
        public nuint KernelTotal;
        public nuint KernelPaged;
        public nuint KernelNonpaged;
        public nuint PageSize;
        public uint  HandleCount;
        public uint  ProcessCount;
        public uint  ThreadCount;
    }

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
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool GetPerformanceInfo(PERFORMANCE_INFORMATION* pPerformanceInformation, uint cb);

    [DllImport(Libraries.PsApi, SetLastError = true)]
    public static extern unsafe bool GetProcessMemoryInfo(
        nint hProcess,
        PROCESS_MEMORY_COUNTERS* psmemCounters,
        uint cb);
}
