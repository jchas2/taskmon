namespace Task.Monitor.System.Services.Process;

// The previous cycle's counters for one pid, retained across cycles so a single enumeration can be
// differenced against the last one. The old Processor kept no state between cycles and so had to
// enumerate every process twice per tick to get the same deltas.
internal sealed class ProcessSampleState
{
    public long KernelTime;
    public long UserTime;
    public ulong DiskReadBytes;
    public ulong DiskWriteBytes;

    // Stopwatch.GetTimestamp() at the moment the counters above were read, so the interval is the
    // one that actually elapsed rather than the nominal Delay.
    public long TimestampTicks;

    public bool Primed;

    // The cycle this pid was last seen on. Stale states are swept by comparing against the current
    // cycle rather than by comparing collection sizes.
    public int Generation;

    public ProcessEntryAverage Average { get; } = new();
}
