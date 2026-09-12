namespace Task.Monitor.System.Services.Process;

// One process as published for a single cycle. The rate fields cover the interval that just
// elapsed; the totals are whatever the process itself reports as cumulative.
public sealed class ProcessEntry
{
    public int Pid { get; set; }
    public int ParentPid { get; set; }
    public int ThreadCount { get; set; }
    public uint HandleCount { get; set; }
    public long BasePriority { get; set; }

    public bool IsDaemon { get; set; }
    public bool IsLowPriority { get; set; }
    public bool IsRunningAsRoot { get; set; }

    public string ProcessName { get; set; } = string.Empty;
    public string FileDescription { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string CmdLine { get; set; } = string.Empty;

    public long UsedMemory { get; set; }

    // Cumulative for the life of the process, as reported by IO_COUNTERS. These count logical I/O
    // including cache hits and non disk handles, so they will not reconcile with DiskService, which
    // samples the device layer. Task Manager's Processes and Performance tabs disagree for the
    // same reason.
    public ulong DiskReadBytes { get; set; }
    public ulong DiskWriteBytes { get; set; }
    public double DiskBytesPerSecond { get; set; }

    public double CpuTimePercent { get; set; }
    public double CpuUserTimePercent { get; set; }
    public double CpuKernelTimePercent { get; set; }

    // Joined from GpuService's published per pid projection, already a ratio. No delta is computed
    // here: the underlying counter is a rate counter that Pdh derives over the true interval.
    public double GpuTimePercent { get; set; }

    public double CpuTimePercentAvg { get; set; }
    public double GpuTimePercentAvg { get; set; }
    public long UsedMemoryAvg { get; set; }
    public double DiskBytesPerSecondAvg { get; set; }

    public double CpuTimePercentMax { get; set; }
    public double GpuTimePercentMax { get; set; }
    public long UsedMemoryMax { get; set; }
    public double DiskBytesPerSecondMax { get; set; }

    // Task-Manager-style qualitative power rating, derived from the activity above. Windows exposes
    // no calibrated per-process wattage.
    public ProcessPowerBucket PowerBucket { get; set; }
}
