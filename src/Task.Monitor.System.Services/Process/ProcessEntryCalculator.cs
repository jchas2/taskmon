using System.Diagnostics;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Process;

public static class ProcessEntryCalculator
{
    public static double CpuPercent(long timeDelta, double totalSystemTime, int irixFactor) =>
        totalSystemTime > 0.0
            ? irixFactor * timeDelta / totalSystemTime
            : 0.0;

    public static double ElapsedSeconds(long fromTicks, long toTicks) =>
        (toTicks - fromTicks) / (double)Stopwatch.Frequency;

    public static int IrixFactor(bool irixMode, int logicalProcessorCount) =>
        irixMode
            ? logicalProcessorCount
            : 1;

    public static double TotalSystemTime(double elapsedSeconds, int logicalProcessorCount) =>
        elapsedSeconds * CounterRate.FileTimeTicksPerSecond * logicalProcessorCount;
}
