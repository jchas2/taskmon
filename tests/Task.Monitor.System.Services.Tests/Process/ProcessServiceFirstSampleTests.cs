using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed class ProcessServiceFirstSampleTests
{
    private const ulong Megabyte = 1024UL * 1024UL;

    private static readonly DateTime PreviousSample = new(2026, 11, 1, 16, 10, 0, DateTimeKind.Utc);

    [Fact]
    public void A_Process_Already_Running_Counts_Nothing_On_Its_First_Sample()
    {
        ProcessSample sample = new() {
            StartTimeUtc   = PreviousSample.AddDays(-1),
            DiskWriteBytes = 50_000 * Megabyte
        };

        Assert.Equal(0.0, ProcessService.FirstDiskBytesPerSecond(sample, PreviousSample, 1.5));
    }

    // Regression test: dd writes 256 MB in about 60 ms, so almost every dd was seen once, by a sample
    // that only recorded its baseline.
    [Fact]
    public void A_Process_Started_Since_The_Last_Sample_Counts_All_Of_Its_Disk_Use()
    {
        ProcessSample sample = new() {
            StartTimeUtc   = PreviousSample.AddSeconds(1),
            DiskReadBytes  = 1 * Megabyte,
            DiskWriteBytes = 255 * Megabyte
        };

        Assert.Equal(128.0 * Megabyte, ProcessService.FirstDiskBytesPerSecond(sample, PreviousSample, 2.0));
    }

    [Fact]
    public void A_Process_With_No_Start_Time_Counts_Nothing_On_Its_First_Sample()
    {
        ProcessSample sample = new() { DiskWriteBytes = 50_000 * Megabyte };

        Assert.Equal(0.0, ProcessService.FirstDiskBytesPerSecond(sample, PreviousSample, 1.5));
    }

    [Fact]
    public void The_Very_First_Sample_Counts_Nothing()
    {
        ProcessSample sample = new() {
            StartTimeUtc   = PreviousSample,
            DiskWriteBytes = 50_000 * Megabyte
        };

        Assert.Equal(0.0, ProcessService.FirstDiskBytesPerSecond(sample, default, 1.5));
    }
}
