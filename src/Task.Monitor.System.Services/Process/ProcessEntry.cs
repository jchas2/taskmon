namespace Task.Monitor.System.Services.Process;

public sealed class ProcessEntry
{
    public int   Pid                    { get; set; }
    public int   ParentPid              { get; set; }
    public int   ThreadCount            { get; set; }
    public uint   HandleCount           { get; set; }
    public long   BasePriority          { get; set; }

    public bool   IsDaemon              { get; set; }
    public bool   IsLowPriority         { get; set; }
    public bool   IsRunningAsRoot       { get; set; }

    public string ProcessName           { get; set; } = string.Empty;
    public string FileDescription       { get; set; } = string.Empty;
    public string UserName              { get; set; } = string.Empty;
    public string CmdLine               { get; set; } = string.Empty;

    public long   UsedMemory            { get; set; }

    public ulong  DiskReadBytes         { get; set; }
    public ulong  DiskWriteBytes        { get; set; }
    public double DiskBytesPerSecond    { get; set; }

    public double CpuTimePercent        { get; set; }
    public double CpuUserTimePercent    { get; set; }
    public double CpuKernelTimePercent  { get; set; }

    public double GpuTimePercent        { get; set; }

    public double CpuTimePercentAvg     { get; set; }
    public double GpuTimePercentAvg     { get; set; }
    public long   UsedMemoryAvg         { get; set; }
    public double DiskBytesPerSecondAvg { get; set; }

    public double CpuTimePercentMax     { get; set; }
    public double GpuTimePercentMax     { get; set; }
    public long   UsedMemoryMax         { get; set; }
    public double DiskBytesPerSecondMax { get; set; }

    public ProcessPowerBucket PowerBucket { get; set; }
}
