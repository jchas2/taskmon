namespace Task.Monitor.System.Services.Common;

public static class CounterRate
{
    // Windows FILETIME values and PDH timestamps count in 100ns ticks.
    public const double FileTimeTicksPerSecond = 10_000_000.0;

    public static double Accumulate(
        long      current,
        ref long  previous,
        ref ulong total,
        double    elapsedSeconds)
    {
        ulong delta = current > previous ? (ulong)(current - previous) : 0;

        total    += delta;
        previous  = current;

        return PerSecond(delta, elapsedSeconds);
    }

    public static double Accumulate(
        ulong     current,
        ref ulong previous,
        ref ulong total,
        double    elapsedSeconds)
    {
        ulong delta = Delta(current, previous);

        total    += delta;
        previous  = current;

        return PerSecond(delta, elapsedSeconds);
    }

    public static ulong Delta(ulong current, ulong previous) =>
        current > previous
            ? current - previous
            : 0;

    public static double PerSecond(double amount, double elapsedSeconds) =>
        elapsedSeconds > 0.0
            ? amount / elapsedSeconds
            : 0.0;
}
