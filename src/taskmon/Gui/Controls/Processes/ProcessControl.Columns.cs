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
        Process = 0,
        Pid,
        User,
        Priority,
        Cpu,
        AvgCpu,
        MaxCpu,
        Threads,
        Gpu,
        AvgGpu,
        MaxGpu,
        Memory,
        AvgMemory,
        MaxMemory,
        Disk,
        AvgDisk,
        MaxDisk,
        Power,
        CommandLine,
        Count
    }

    internal static string Title(Columns column) => column switch {
        Columns.Process     => Strings.ProcessColumnProcess,
        Columns.Pid         => Strings.ProcessColumnPid,
        Columns.User        => Strings.ProcessColumnUser,
#if __APPLE__
        Columns.Priority    => Strings.ProcessColumnPriorityMacOS,
#else
        Columns.Priority    => Strings.ProcessColumnPriorityWindows,
#endif
        Columns.Cpu         => Strings.ProcessColumnCpu,
        Columns.AvgCpu      => Strings.ProcessColumnAvgCpu,
        Columns.MaxCpu      => Strings.ProcessColumnMaxCpu,
        Columns.Threads     => Strings.ProcessColumnThreads,
        Columns.Gpu         => Strings.ProcessColumnGpu,
        Columns.AvgGpu      => Strings.ProcessColumnAvgGpu,
        Columns.MaxGpu      => Strings.ProcessColumnMaxGpu,
        Columns.Memory      => Strings.ProcessColumnMemory,
        Columns.AvgMemory   => Strings.ProcessColumnAvgMemory,
        Columns.MaxMemory   => Strings.ProcessColumnMaxMemory,
        Columns.Disk        => Strings.ProcessColumnDisk,
        Columns.AvgDisk     => Strings.ProcessColumnAvgDisk,
        Columns.MaxDisk     => Strings.ProcessColumnMaxDisk,
        Columns.Power       => Strings.ProcessColumnPower,
        Columns.CommandLine => Strings.ProcessColumnPath,
                          _ => string.Empty
    };
}
