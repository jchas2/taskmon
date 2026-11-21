using System.Diagnostics;
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
        bool listed = QueryMemoryList(out Winternl.SYSTEM_MEMORY_LIST_INFORMATION memListInfo, out int status);

        if (IsFirstCycle) {
            Trace.WriteLine(listed
                ? $"{ServiceName}: NtQuerySystemInformation memory lists OK"
                : $"{ServiceName}: NtQuerySystemInformation memory lists FAILED {ErrorCodes.Describe(status)}");
        }

        if (!listed) {
            TraceEx.WriteLineOnce(nameof(QueryMemoryList), $"Failed to query NtQuerySystemInformation: {ErrorCodes.Describe(status)}");
            return;
        }

        ulong totalBytes = memoryInfo.Metrics.TotalPhysical;
        nuint pageSize   = (nuint)Environment.SystemPageSize;

        if (totalBytes == 0) {
            return;
        }

        ulong freeBytes     = (memListInfo.FreePageCount + memListInfo.ZeroedPageCount) * pageSize;
        ulong modifiedBytes = (memListInfo.ModifiedPageCount + memListInfo.ModifiedNoWritePageCount) * pageSize;

        nuint standbyPageCount = 0;
        
        for (int i = 0; i < 8; i++) {
            standbyPageCount += memListInfo.PageCountByPriority[i];
        }
        
        ulong standbyBytes = standbyPageCount * pageSize;

        ulong inUseBytes = totalBytes - (standbyBytes + freeBytes + modifiedBytes);

        memoryInfo.Metrics.InUseBytes    = inUseBytes;
        memoryInfo.Metrics.ModifiedBytes = modifiedBytes;
        memoryInfo.Metrics.StandbyBytes  = standbyBytes;
        memoryInfo.Metrics.FreeBytes     = freeBytes;

        memoryInfo.Metrics.InUseBytesRatio    = (double)inUseBytes / totalBytes;
        memoryInfo.Metrics.ModifiedBytesRatio = (double)modifiedBytes / totalBytes;
        memoryInfo.Metrics.StandbyBytesRatio  = (double)standbyBytes / totalBytes;
        memoryInfo.Metrics.FreeBytesRatio     = (double)freeBytes / totalBytes;
    }

    private unsafe void OnDoWorkMemoryMetrics(MemoryInfo memoryInfo)
    {
        SysInfoApi.MEMORYSTATUSEX memoryStatus = new();
        
        if (!SysInfoApi.GlobalMemoryStatusEx(&memoryStatus)) {
            PInvokeErrorHelpers.AssertOnLastError(nameof(SysInfoApi.GlobalMemoryStatusEx));
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(SysInfoApi.GlobalMemoryStatusEx), 
                $"Failed {nameof(OnDoWorkMemoryMetrics)}");

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

    partial void OnStartPressureMetrics()
    {
        pagingQuery = PdhQuery.OpenAtStartup(
            ServiceName,
            "paging counters",
            @"\Memory\Pages Input/sec",
            @"\Memory\Pages Output/sec",
            @"\Memory\Page Faults/sec");

        pagingQuery?.Collect();
    }

    partial void OnStopPressureMetrics()
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
        using HGlobalScope buffer = HGlobalScope.Allocate((int)returnLength);

        new Span<byte>((void*)buffer.Value, (int)returnLength).Clear();

        status = Winternl.NtQuerySystemInformation(
            Winternl.SystemMemoryListInformation,
            (void*)buffer.Value,
            returnLength,
            &returnLength);

        if (status != Winternl.STATUS_SUCCESS) {
            return false;
        }

        info = Unsafe.ReadUnaligned<Winternl.SYSTEM_MEMORY_LIST_INFORMATION>((void*)buffer.Value);
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
