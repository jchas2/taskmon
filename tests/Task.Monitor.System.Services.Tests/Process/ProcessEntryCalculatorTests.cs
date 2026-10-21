using System.Diagnostics;
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed class ProcessEntryCalculatorTests
{
    [Fact]
    public void CpuPercent_Is_The_Share_Of_Available_Cpu_Time()
    {
        double totalSystemTime = ProcessEntryCalculator.TotalSystemTime(1.0, 8);

        // One core fully consumed for one second out of eight available cores is one eighth.
        long oneCoreSecond = (long)CounterRate.FileTimeTicksPerSecond;

        Assert.Equal(
            0.125,
            ProcessEntryCalculator.CpuPercent(oneCoreSecond, totalSystemTime, irixFactor: 1),
            precision: 6);

        // The same delta in Irix mode reads as one saturated core.
        Assert.Equal(
            1.0,
            ProcessEntryCalculator.CpuPercent(oneCoreSecond, totalSystemTime, irixFactor: 8),
            precision: 6);
    }

    [Fact]
    public void CpuPercent_Returns_Zero_Rather_Than_Dividing_By_Zero()
    {
        double result = ProcessEntryCalculator.CpuPercent(12345, totalSystemTime: 0.0, irixFactor: 1);

        Assert.Equal(0.0, result);
        Assert.False(double.IsNaN(result));
        Assert.False(double.IsInfinity(result));
    }

    [Fact]
    public void ElapsedSeconds_Converts_From_The_Stopwatch_Timebase()
    {
        long from = 1_000_000;
        long to = from + (Stopwatch.Frequency / 2);

        Assert.Equal(0.5, ProcessEntryCalculator.ElapsedSeconds(from, to), precision: 6);
    }

    [Fact]
    public void IrixFactor_Measures_Against_One_Core_Or_All_Cores()
    {
        // Irix reports 100% as one saturated core, matching macOS Activity Monitor.
        Assert.Equal(16, ProcessEntryCalculator.IrixFactor(irixMode: true, logicalProcessorCount: 16));

        // Non Irix reports 100% as every core saturated, matching Windows Task Manager.
        Assert.Equal(1, ProcessEntryCalculator.IrixFactor(irixMode: false, logicalProcessorCount: 16));
    }

    [Fact]
    public void TotalSystemTime_Scales_With_The_Elapsed_Interval_Not_A_Nominal_Delay()
    {
        // One second across 8 cores is 8 seconds of cpu time, in 100ns FILETIME ticks.
        Assert.Equal(
            8 * CounterRate.FileTimeTicksPerSecond,
            ProcessEntryCalculator.TotalSystemTime(elapsedSeconds: 1.0, logicalProcessorCount: 8));

        // The old Processor divided by Environment.ProcessorCount * Delay.Ticks regardless of how
        // long the cycle really took. A cycle that overran by 20% must widen the denominator by
        // 20%, otherwise every percentage is inflated by the overrun.
        Assert.Equal(
            9.6 * CounterRate.FileTimeTicksPerSecond,
            ProcessEntryCalculator.TotalSystemTime(elapsedSeconds: 1.2, logicalProcessorCount: 8),
            precision: 6);
    }
}
