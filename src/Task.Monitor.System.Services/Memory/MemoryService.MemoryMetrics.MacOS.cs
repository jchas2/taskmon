#if __APPLE__
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Memory;

public partial class MemoryService
{
    private MachHost.VmStatistics64 vmStats;
    private ulong totalPhysical;
    private long pageSize;

    private void OnDoWorkMemoryMetrics(MemoryInfo memoryInfo)
    {
        if (!TryReadVmStatistics(out vmStats)) {
            return;
        }

        pageSize = Environment.SystemPageSize;
        totalPhysical = Sys.SysctlByNameLong("hw.memsize", out long memSize) && memSize > 0
            ? (ulong)memSize
            : 0;

        ulong usedPages = vmStats.wire_count + vmStats.inactive_count + vmStats.active_count +
            vmStats.compressor_page_count;
        ulong usedBytes = usedPages * (ulong)pageSize;

        memoryInfo.Metrics.TotalPhysical = totalPhysical;
        memoryInfo.Metrics.AvailablePhysical = totalPhysical > usedBytes
            ? totalPhysical - usedBytes
            : 0;

        memoryInfo.Metrics.TotalVirtual = 0;
        memoryInfo.Metrics.AvailableVirtual = 0;

        ReadSwapUsage(memoryInfo);

        memoryInfo.Metrics.AvailablePhysicalRatio = totalPhysical > 0
            ? 1.0 - memoryInfo.Metrics.AvailablePhysical / (double)totalPhysical
            : 0.0;

        memoryInfo.Metrics.AvailablePageFileRatio = memoryInfo.Metrics.TotalPageFile > 0
            ? 1.0 - memoryInfo.Metrics.AvailablePageFile / (double)memoryInfo.Metrics.TotalPageFile
            : 0.0;

        memoryInfo.Metrics.AvailableVirtualRatio = 0.0;
    }

    private void OnDoWorkMemoryCompressionMetrics(MemoryInfo memoryInfo)
    {
        if (totalPhysical == 0 || pageSize == 0) {
            return;
        }

        ulong page = (ulong)pageSize;

        ulong freeBytes = vmStats.free_count * page;
        ulong standbyBytes = vmStats.inactive_count * page;
        ulong modifiedBytes = vmStats.compressor_page_count * page;
        ulong accounted = freeBytes + standbyBytes + modifiedBytes;
        ulong inUseBytes = totalPhysical > accounted ? totalPhysical - accounted : 0;

        memoryInfo.Metrics.FreeBytes = freeBytes;
        memoryInfo.Metrics.StandbyBytes = standbyBytes;
        memoryInfo.Metrics.ModifiedBytes = modifiedBytes;
        memoryInfo.Metrics.InUseBytes = inUseBytes;

        memoryInfo.Metrics.FreeBytesRatio = (double)freeBytes / totalPhysical;
        memoryInfo.Metrics.StandbyBytesRatio = (double)standbyBytes / totalPhysical;
        memoryInfo.Metrics.ModifiedBytesRatio = (double)modifiedBytes / totalPhysical;
        memoryInfo.Metrics.InUseBytesRatio = (double)inUseBytes / totalPhysical;
    }

    private static bool TryReadVmStatistics(out MachHost.VmStatistics64 info)
    {
        info = default;

        IntPtr host = MachHost.host_self();
        int count = Marshal.SizeOf<MachHost.VmStatistics64>() / sizeof(int);
        using HGlobalScope buffer = HGlobalScope.Allocate(Marshal.SizeOf<MachHost.VmStatistics64>());

        if (MachHost.host_statistics64(host, MachHost.HOST_VM_INFO64, buffer, ref count) != 0) {
            return false;
        }

        info = Marshal.PtrToStructure<MachHost.VmStatistics64>(buffer);
        return true;
    }

    private static unsafe void ReadSwapUsage(MemoryInfo memoryInfo)
    {
        ReadOnlySpan<int> name = [(int)Sys.Selectors.CTL_VM, Sys.VM_SWAPUSAGE];
        byte* buffer = null;
        int length = 0;

        bool read = Sys.Sysctl(name, ref buffer, ref length);
        using HGlobalScope scope = new((nint)buffer);

        if (!read || length != sizeof(Sys.XswUsage)) {
            return;
        }

        Sys.XswUsage* xsw = (Sys.XswUsage*)buffer;
        memoryInfo.Metrics.TotalPageFile = xsw->total;
        memoryInfo.Metrics.AvailablePageFile = xsw->avail;
    }
}
#endif
