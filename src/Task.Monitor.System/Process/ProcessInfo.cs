namespace Task.Monitor.System.Process;

public sealed class ProcessInfo
{
    public int Pid { get; internal set; }
    public int ParentPid { get; internal set; }
    public string ProcessName { get; internal set; } = string.Empty;
    public string ModuleName { get; internal set; } = string.Empty;
    public string FileName { get; internal set; } = string.Empty;
    public string FileDescription { get; internal set; } = string.Empty;
    public bool IsDaemon { get; internal set; }
    public bool IsLowPriority { get; internal set; }
    public string UserName { get; internal set; } = string.Empty;
    public string CmdLine { get; internal set; } = string.Empty;
    public int ThreadCount { get; internal set; }
    public uint HandleCount { get; internal set; }
    public long BasePriority { get; internal set; }
    public long UsedMemory { get; internal set; }
    public long KernelTime { get; internal set; }
    public long UserTime { get; internal set; }
    public long GpuTime { get; internal set; }
    public ulong DiskReadBytes { get; internal set; }
    public ulong DiskWriteBytes { get; internal set; }
    public ulong DiskOperations { get; internal set; }
}

