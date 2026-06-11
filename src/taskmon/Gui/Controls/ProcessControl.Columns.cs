using System.ComponentModel;

namespace Task.Monitor.Gui.Controls;

public partial class ProcessControl
{
    internal const int ColumnProcessWidth = 32;
    internal const int ColumnPidWidth = 7;
    internal const int ColumnUserWidth = 16;
    internal const int ColumnPriorityWidth = 4;
    internal const int ColumnCpuWidth = 7;
    internal const int ColumnThreadsWidth = 7;
    internal const int ColumnGpuWidth = 7;
    internal const int ColumnMemoryWidth = 10;
    internal const int ColumnDiskWidth = 12;
    internal const int ColumnCommandlineWidth = 32;

    public enum Columns
    {
        [ColumnTitle("PROCESS")]
        [ColumnProperty("FileDescription")]
        Process = 0,
        [ColumnTitle("PID")]
        [ColumnProperty("Pid")]
        [ColumnSortKey(ConsoleKey.N)]
        Pid,
        [ColumnTitle("USER")]
        [ColumnProperty("UserName")]
        [ColumnSortKey(ConsoleKey.U)]
        User,
#if __WIN32__        
        [ColumnTitle("PRI")]
#elif __APPLE__
        [ColumnTitle("NI")]
#endif
        [ColumnProperty("BasePriority")]
        Priority,
        [ColumnTitle("CPU%")]
        [ColumnProperty("CpuTimePercent")]
        [ColumnSortKey(ConsoleKey.P)]
        Cpu,
        [ColumnTitle("THRDS")]
        [ColumnProperty("ThreadCount")]
        Threads,
        [ColumnTitle("GPU%")]
        [ColumnProperty("GpuTimePercent")]
        [ColumnSortKey(ConsoleKey.G)]
        Gpu,
        [ColumnTitle("MEM")]
        [ColumnProperty("UsedMemory")]
        [ColumnSortKey(ConsoleKey.M)]
        Memory,
        [ColumnTitle("DISK")]
        [ColumnProperty("DiskUsage")]
        [ColumnSortKey(ConsoleKey.D)]
        Disk,
        [ColumnTitle("PATH")]
        [ColumnProperty("CmdLine")]
        CommandLine,
        [ColumnTitle("")]
        [ColumnProperty("")]
        Count
    }
}
