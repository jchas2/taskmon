using System.ComponentModel;

namespace Task.Monitor.Configuration;

[Flags]
public enum Statistics
{
    Process = 1 << 0,
    Pid     = 1 << 1,
    User    = 1 << 2,
    Pri     = 1 << 3,
    Cpu     = 1 << 4,
    Thrd    = 1 << 5,
    Gpu     = 1 << 6,
    Mem     = 1 << 7,
    Path    = 1 << 8,
    Disk    = 1 << 9,
    AvgCpu  = 1 << 10,
    AvgGpu  = 1 << 11,
    AvgMem  = 1 << 12,
    AvgDisk = 1 << 13
}
