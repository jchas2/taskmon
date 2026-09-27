namespace Task.Monitor.System.Services.Process;

internal sealed class ProcessSampleState
{
    public long  KernelTime;
    public long  UserTime;
    public ulong DiskReadBytes;
    public ulong DiskWriteBytes;
    
    public long  TimestampTicks;
    public bool  Primed;
    public int   Generation;
    public ProcessEntryAverage Average { get; } = new();
}
