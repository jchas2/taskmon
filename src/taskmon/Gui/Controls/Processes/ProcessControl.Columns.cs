using System.ComponentModel;
using Task.Monitor.Resources;

namespace Task.Monitor.Gui.Controls.Processes;

public partial class ProcessControl
{
    internal const int ColumnProcessWidth     = 32;
    internal const int ColumnPidWidth         = 7;
    internal const int ColumnUserWidth        = 16;
    internal const int ColumnPriorityWidth    = 5;
    internal const int ColumnCpuWidth         = 7;
    internal const int ColumnAvgCpuWidth      = 10;
    internal const int ColumnMaxCpuWidth      = 10;
    internal const int ColumnThreadsWidth     = 7;
    internal const int ColumnGpuWidth         = 7;
    internal const int ColumnAvgGpuWidth      = 10;
    internal const int ColumnMaxGpuWidth      = 10;
    internal const int ColumnMemoryWidth      = 10;
    internal const int ColumnAvgMemoryWidth   = 10;
    internal const int ColumnMaxMemoryWidth   = 10;
    internal const int ColumnDiskWidth        = 12;
    internal const int ColumnAvgDiskWidth     = 12;
    internal const int ColumnMaxDiskWidth     = 12;
    internal const int ColumnPowerWidth       = 11;
    internal const int ColumnCommandlineWidth = 32;

    public enum Columns
    {
        [ColumnTitle(nameof(Strings.ProcessColumnProcess))]
        [ColumnProperty("FileDescription")]
        Process = 0,
        [ColumnTitle(nameof(Strings.ProcessColumnPid))]
        [ColumnProperty("Pid")]
        [ColumnSortKey(ConsoleKey.N)]
        Pid,
        [ColumnTitle(nameof(Strings.ProcessColumnUser))]
        [ColumnProperty("UserName")]
        [ColumnSortKey(ConsoleKey.U)]
        User,
#if __WIN32__        
        [ColumnTitle(nameof(Strings.ProcessColumnPriorityWindows))]
#elif __APPLE__
        [ColumnTitle(nameof(Strings.ProcessColumnPriorityMacOS))]
#endif
        [ColumnProperty("BasePriority")]
        Priority,
        [ColumnTitle(nameof(Strings.ProcessColumnCpu))]
        [ColumnProperty("CpuTimePercent")]
        [ColumnSortKey(ConsoleKey.P)]
        Cpu,
        [ColumnTitle(nameof(Strings.ProcessColumnAvgCpu))]
        [ColumnProperty("CpuTimePercentAvg")]
        AvgCpu,
        [ColumnTitle(nameof(Strings.ProcessColumnMaxCpu))]
        [ColumnProperty("CpuTimePercentMax")]
        MaxCpu,
        [ColumnTitle(nameof(Strings.ProcessColumnThreads))]
        [ColumnProperty("ThreadCount")]
        Threads,
        [ColumnTitle(nameof(Strings.ProcessColumnGpu))]
        [ColumnProperty("GpuTimePercent")]
        [ColumnSortKey(ConsoleKey.G)]
        Gpu,
        [ColumnTitle(nameof(Strings.ProcessColumnAvgGpu))]
        [ColumnProperty("GpuTimePercentAvg")]
        AvgGpu,
        [ColumnTitle(nameof(Strings.ProcessColumnMaxGpu))]
        [ColumnProperty("GpuTimePercentMax")]
        MaxGpu,
        [ColumnTitle(nameof(Strings.ProcessColumnMemory))]
        [ColumnProperty("UsedMemory")]
        [ColumnSortKey(ConsoleKey.M)]
        Memory,
        [ColumnTitle(nameof(Strings.ProcessColumnAvgMemory))]
        [ColumnProperty("UsedMemoryAvg")]
        AvgMemory,
        [ColumnTitle(nameof(Strings.ProcessColumnMaxMemory))]
        [ColumnProperty("UsedMemoryMax")]
        MaxMemory,
        [ColumnTitle(nameof(Strings.ProcessColumnDisk))]
        [ColumnProperty("DiskUsage")]
        [ColumnSortKey(ConsoleKey.D)]
        Disk,
        [ColumnTitle(nameof(Strings.ProcessColumnAvgDisk))]
        [ColumnProperty("DiskUsageAvg")]
        AvgDisk,
        [ColumnTitle(nameof(Strings.ProcessColumnMaxDisk))]
        [ColumnProperty("DiskUsageMax")]
        MaxDisk,
        [ColumnTitle(nameof(Strings.ProcessColumnPower))]
        [ColumnProperty("PowerBucket")]
        Power,
        [ColumnTitle(nameof(Strings.ProcessColumnPath))]
        [ColumnProperty("CmdLine")]
        CommandLine,
        [ColumnTitle("")]
        [ColumnProperty("")]
        Count
    }
}
