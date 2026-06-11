namespace Task.Monitor.Process;

public class ProcessorInfo
{
    public int Pid { get; set; }
    public int ThreadCount { get; set; }
    public uint HandleCount { get; set; }
    public long BasePriority { get; set; }
    public int ParentPid { get; set; }
    public bool IsDaemon { get; set; }
    public bool IsLowPriority { get; set; }
    public DateTime StartTime { get; set; }

    public string ProcessName { get; set; } = string.Empty;
    public string FileDescription { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string CmdLine { get; set; } = string.Empty;
    
    public ulong DiskReadBytes { get; internal set; }
    public ulong DiskWriteBytes { get; internal set; }
    public long DiskUsage { get; set; }

    public long UsedMemory { get; set; }

    public double CpuTimePercent { get; set; }
    public double CpuUserTimePercent { get; set; }
    public double CpuKernelTimePercent { get; set; }
    
    public double GpuTimePercent { get; set; }
}