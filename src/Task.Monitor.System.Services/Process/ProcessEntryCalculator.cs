using System.Diagnostics;

namespace Task.Monitor.System.Services.Process;

// The per process rate maths, kept separate from the sampling so it can be exercised without a
// live machine underneath it. This is where the old Processor's arithmetic errors lived.
public static class ProcessEntryCalculator
{
    // FILETIME ticks are 100ns units, so one second of one core's time is this many.
    public const double FileTimeTicksPerSecond = 10_000_000.0;

    public static double ElapsedSeconds(long fromTicks, long toTicks) =>
        (toTicks - fromTicks) / (double)Stopwatch.Frequency;

    // Irix mode measures against a single core, non Irix against every core.
    public static int IrixFactor(bool irixMode, int logicalProcessorCount) =>
        irixMode
            ? logicalProcessorCount
            : 1;

    // The cpu time available across every core over the interval that actually elapsed. The old
    // Processor used Environment.ProcessorCount * Delay.Ticks, which ignored however long the two
    // enumerations themselves took and so over reported every percentage on a busy machine.
    public static double TotalSystemTime(double elapsedSeconds, int logicalProcessorCount) =>
        elapsedSeconds * FileTimeTicksPerSecond * logicalProcessorCount;

    public static double CpuPercent(long timeDelta, double totalSystemTime, int irixFactor) =>
        totalSystemTime > 0.0
            ? irixFactor * timeDelta / totalSystemTime
            : 0.0;

    public static double BytesPerSecond(ulong byteDelta, double elapsedSeconds) =>
        elapsedSeconds > 0.0
            ? byteDelta / elapsedSeconds
            : 0.0;

    // IO_COUNTERS are cumulative and monotonic for a live process, but the subtraction is guarded
    // anyway: an unsigned wrap would turn a small discrepancy into a value near ulong.MaxValue.
    public static ulong Delta(ulong current, ulong previous) =>
        current > previous
            ? current - previous
            : 0;
}
