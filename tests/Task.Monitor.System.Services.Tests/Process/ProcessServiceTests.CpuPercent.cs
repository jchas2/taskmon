using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed partial class ProcessServiceTests
{
    private static readonly long OneCoreSecond = (long)CounterRate.FileTimeTicksPerSecond;

    [Fact]
    public void CpuPercent_Is_The_Share_Of_Available_Cpu_Time()
    {
        ProcessSpecs specs = new() { LogicalProcessorCount = 8, IrixMode = false };

        // One core fully consumed for one second out of eight available cores is one eighth.
        Assert.Equal(0.125, ProcessService.CpuPercent(OneCoreSecond, elapsedSeconds: 1.0, specs), precision: 6);

        // The same delta in Irix mode reads as one saturated core, as macOS Activity Monitor shows it.
        specs.IrixMode = true;

        Assert.Equal(1.0, ProcessService.CpuPercent(OneCoreSecond, elapsedSeconds: 1.0, specs), precision: 6);
    }

    [Fact]
    public void CpuPercent_Returns_Zero_Rather_Than_Dividing_By_Zero()
    {
        double result = ProcessService.CpuPercent(12345, elapsedSeconds: 0.0, new ProcessSpecs());

        Assert.Equal(0.0, result);
    }

    // Dividing by a nominal delay rather than the time the cycle really took inflates every
    // percentage when a cycle overruns: a cycle 20% late must widen the denominator by 20%.
    [Fact]
    public void CpuPercent_Scales_With_The_Elapsed_Interval_Not_A_Nominal_Delay()
    {
        ProcessSpecs specs = new() { LogicalProcessorCount = 8, IrixMode = false };

        Assert.Equal(0.125 / 1.2, ProcessService.CpuPercent(OneCoreSecond, elapsedSeconds: 1.2, specs), precision: 6);
    }
}
