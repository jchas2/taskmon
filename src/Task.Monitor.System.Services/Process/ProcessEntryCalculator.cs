using System.Diagnostics;

namespace Task.Monitor.System.Services.Process;

public static class ProcessEntryCalculator
{
    public const double FileTimeTicksPerSecond = 10_000_000.0;

    public static double ElapsedSeconds(long fromTicks, long toTicks) =>
        (toTicks - fromTicks) / (double)Stopwatch.Frequency;

    public static int IrixFactor(bool irixMode, int logicalProcessorCount) =>
        irixMode
            ? logicalProcessorCount
            : 1;

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

    public static ulong Delta(ulong current, ulong previous) =>
        current > previous
            ? current - previous
            : 0;
}
