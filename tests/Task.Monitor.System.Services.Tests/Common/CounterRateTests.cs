using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Tests.Common;

public sealed class CounterRateTests
{
    [Fact]
    public void Accumulate_Adds_The_Increase_To_The_Total_And_Moves_Previous_On()
    {
        ulong previous = 1000;
        ulong total    = 50;

        double rate = CounterRate.Accumulate(current: 3000UL, ref previous, ref total, elapsedSeconds: 2.0);

        Assert.Equal(1000.0, rate);
        Assert.Equal(2050UL, total);
        Assert.Equal(3000UL, previous);
    }

    [Fact]
    public void Accumulate_Ignores_A_Counter_That_Went_Backwards()
    {
        // A reset or replaced device restarts its counter; that is no traffic, not a huge jump.
        long  previous = 5000;
        ulong total    = 50;

        double rate = CounterRate.Accumulate(current: 100L, ref previous, ref total, elapsedSeconds: 1.0);

        Assert.Equal(0.0, rate);
        Assert.Equal(50UL, total);
        Assert.Equal(100L, previous);
    }

    [Fact]
    public void Delta_Clamps_Instead_Of_Wrapping_Unsigned()
    {
        Assert.Equal(512UL, CounterRate.Delta(current: 1536, previous: 1024));

        // A counter that appears to move backwards must read as no traffic, not as a value near
        // ulong.MaxValue.
        Assert.Equal(0UL, CounterRate.Delta(current: 1024, previous: 1536));
    }

    [Fact]
    public void PerSecond_Divides_By_The_Elapsed_Interval()
    {
        Assert.Equal(2048.0, CounterRate.PerSecond(1024, elapsedSeconds: 0.5));
    }

    [Fact]
    public void PerSecond_Returns_Zero_On_A_Zero_Interval()
    {
        // The priming cycle has no interval behind it, so it must report nothing rather than an
        // infinite rate.
        double result = CounterRate.PerSecond(1024, elapsedSeconds: 0.0);

        Assert.Equal(0.0, result);
        Assert.False(double.IsInfinity(result));
    }
}
