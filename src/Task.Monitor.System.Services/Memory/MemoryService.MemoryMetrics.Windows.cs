using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Memory;

public partial class MemoryService
{
#if __WIN32__
    private PdhQuery? pagingQuery;

    private unsafe void OnDoWorkMemoryCompressionMetrics(MemoryInfo memoryInfo)
    {
        if (!QueryMemoryList(out Winternl.SYSTEM_MEMORY_LIST_INFORMATION memListInfo, out int status)) {
            TraceEx.WriteLineOnce(nameof(QueryMemoryList), $"Failed to query NtQuerySystemInformation. Status: 0x{status:X8}");
            return;
        }

        SysInfoApi.MEMORYSTATUSEX memStatus = new();
        memStatus.dwLength = (uint)sizeof(SysInfoApi.MEMORYSTATUSEX);
        
        if (!SysInfoApi.GlobalMemoryStatusEx(&memStatus)) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(SysInfoApi.GlobalMemoryStatusEx), 
                $"GlobalMemoryStatusEx failed. Error: {Marshal.GetLastWin32Error()}");
            
            return;
        }

        SysInfoApi.SYSTEM_INFO sysInfo = new();
        SysInfoApi.GetSystemInfo(&sysInfo);
        nuint pageSize = sysInfo.dwPageSize;

        ulong freeBytes     = (memListInfo.FreePageCount + memListInfo.ZeroedPageCount) * pageSize;
        ulong modifiedBytes = (memListInfo.ModifiedPageCount + memListInfo.ModifiedNoWritePageCount) * pageSize;

        // Standby bytes across all 8 priority levels (0 to 7)
        nuint standbyPageCount = 0;
        
        for (int i = 0; i < 8; i++) {
            standbyPageCount += memListInfo.PageCountByPriority[i];
        }
        
        ulong standbyBytes = standbyPageCount * pageSize;

        ulong totalBytes = memStatus.ullTotalPhys;
        ulong inUseBytes = totalBytes - (standbyBytes + freeBytes + modifiedBytes);

        memoryInfo.Metrics.InUseBytes    = inUseBytes;
        memoryInfo.Metrics.ModifiedBytes = modifiedBytes;
        memoryInfo.Metrics.StandbyBytes  = standbyBytes;
        memoryInfo.Metrics.FreeBytes     = freeBytes;

        if (totalBytes > 0) {
            memoryInfo.Metrics.InUseBytesRatio    = (double)inUseBytes / (double)totalBytes;
            memoryInfo.Metrics.ModifiedBytesRatio = (double)modifiedBytes / (double)totalBytes;
            memoryInfo.Metrics.StandbyBytesRatio  = (double)standbyBytes / (double)totalBytes;
            memoryInfo.Metrics.FreeBytesRatio     = (double)freeBytes / (double)totalBytes;
        }
    }

    private unsafe void OnDoWorkMemoryMetrics(MemoryInfo memoryInfo)
    {
        SysInfoApi.MEMORYSTATUSEX memoryStatus = new();
        
        if (!SysInfoApi.GlobalMemoryStatusEx(&memoryStatus)) {
            PInvokeErrorHelpers.AssertOnLastError(nameof(SysInfoApi.GlobalMemoryStatusEx));
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(SysInfoApi.GlobalMemoryStatusEx), 
                $"Failed {nameof(OnDoWorkMemoryMetrics)}");
            
            memoryInfo.Metrics.AvailablePageFile      = 0;
            memoryInfo.Metrics.AvailablePhysical      = 0;
            memoryInfo.Metrics.AvailableVirtual       = 0;
            memoryInfo.Metrics.TotalPageFile          = 0;
            memoryInfo.Metrics.TotalPhysical          = 0;
            memoryInfo.Metrics.TotalVirtual           = 0;
            memoryInfo.Metrics.AvailablePageFileRatio = 0.0;
            memoryInfo.Metrics.AvailablePhysicalRatio = 0.0;
            memoryInfo.Metrics.AvailableVirtualRatio  = 0.0;
            return;
        }

        memoryInfo.Metrics.AvailablePageFile = memoryStatus.ullAvailPageFile;
        memoryInfo.Metrics.AvailablePhysical = memoryStatus.ullAvailPhys;
        memoryInfo.Metrics.AvailableVirtual  = memoryStatus.ullAvailVirtual;
        memoryInfo.Metrics.TotalPageFile     = memoryStatus.ullTotalPageFile;
        memoryInfo.Metrics.TotalPhysical     = memoryStatus.ullTotalPhys;
        memoryInfo.Metrics.TotalVirtual      = memoryStatus.ullTotalVirtual;

        memoryInfo.Metrics.AvailablePageFileRatio = memoryStatus.ullAvailPageFile > 0
            ? 1.0 - memoryStatus.ullAvailPageFile / (double)memoryStatus.ullTotalPageFile
            : 0.0;
        
        memoryInfo.Metrics.AvailablePhysicalRatio = memoryStatus.ullTotalPhys > 0
            ? 1.0 - memoryStatus.ullAvailPhys / (double)memoryStatus.ullTotalPhys
            : 0.0;
        
        memoryInfo.Metrics.AvailableVirtualRatio = memoryStatus.ullTotalVirtual > 0
            ? 1.0 - memoryStatus.ullAvailVirtual / (double)memoryStatus.ullTotalVirtual
            : 0.0;
    }

    private void OnDoWorkPressureMetrics(MemoryInfo memoryInfo)
    {
        ReadPerformanceInfo(memoryInfo);
        ReadPagingRates(memoryInfo);
    }

    private void OnStartPressureMetrics()
    {
        // Collected once up front: a rate counter needs two samples before it reports a value.
        pagingQuery = PdhQuery.Open(
            nameof(MemoryService),
            @"\Memory\Pages Input/sec",
            @"\Memory\Pages Output/sec",
            @"\Memory\Page Faults/sec");

        pagingQuery?.Collect();
    }

    private void OnStopPressureMetrics()
    {
        pagingQuery?.Dispose();
        pagingQuery = null;
    }

    private static unsafe bool QueryMemoryList(out Winternl.SYSTEM_MEMORY_LIST_INFORMATION info, out int status)
    {
        info = new();
        int size = sizeof(Winternl.SYSTEM_MEMORY_LIST_INFORMATION);
        uint returnLength = 0;

        fixed (Winternl.SYSTEM_MEMORY_LIST_INFORMATION* p = &info) {
            status = Winternl.NtQuerySystemInformation(
                Winternl.SystemMemoryListInformation, 
                p, 
                (uint)size, 
                &returnLength);
        }

        if (status == Winternl.STATUS_SUCCESS) {
            return true;
        }

        if (status != Winternl.STATUS_INFO_LENGTH_MISMATCH || returnLength < size) {
            return false;
        }

        // Kernel wants a bigger buffer than our struct.
        void* buffer = NativeMemory.AllocZeroed(returnLength);
        status = Winternl.NtQuerySystemInformation(
            Winternl.SystemMemoryListInformation, 
            buffer, 
            returnLength, 
            &returnLength);
        
        if (status != Winternl.STATUS_SUCCESS) {
            NativeMemory.Free(buffer);
            return false;
        }

        info = Unsafe.ReadUnaligned<Winternl.SYSTEM_MEMORY_LIST_INFORMATION>(buffer);
        NativeMemory.Free(buffer);
        return true;
    }

    private void ReadPagingRates(MemoryInfo memoryInfo)
    {
        if (pagingQuery is null || !pagingQuery.Collect()) {
            return;
        }

        if (pagingQuery.TryReadFormattedDouble(pagingQuery.Counters[0], out double pagesInput)) {
            memoryInfo.Metrics.PagesInPerSecond     = pagesInput;
            memoryInfo.Metrics.SwapInBytesPerSecond = pagesInput * Environment.SystemPageSize;
        }

        if (pagingQuery.TryReadFormattedDouble(pagingQuery.Counters[1], out double pagesOutput)) {
            memoryInfo.Metrics.SwapOutBytesPerSecond = pagesOutput * Environment.SystemPageSize;
        }

        if (pagingQuery.TryReadFormattedDouble(pagingQuery.Counters[2], out double pageFaults)) {
            memoryInfo.Metrics.PageFaultsPerSecond = pageFaults;
        }
    }

    private static unsafe void ReadPerformanceInfo(MemoryInfo memoryInfo)
    {
        PsApi.PERFORMANCE_INFORMATION info = new();
        uint size = (uint)sizeof(PsApi.PERFORMANCE_INFORMATION);

        info.cb = size;

        if (!PsApi.GetPerformanceInfo(&info, size)) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(PsApi.GetPerformanceInfo));
            return;
        }

        double pageSize = info.PageSize;

        memoryInfo.Metrics.CommitLimit        = info.CommitLimit * pageSize;
        memoryInfo.Metrics.CommitTotal        = info.CommitTotal * pageSize;
        memoryInfo.Metrics.KernelNonPagedPool = info.KernelNonpaged * pageSize;
        memoryInfo.Metrics.KernelPagedPool    = info.KernelPaged * pageSize;
        memoryInfo.Metrics.SystemCache        = info.SystemCache * pageSize;
    }
#endif
}
