using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed class ProcessPowerScoreTests
{
    [Fact]
    public void An_Idle_Process_Is_Very_Low()
    {
        Assert.Equal(ProcessPowerBucket.VeryLow, ProcessPowerScore.Classify(0, 0, 0));
    }

    [Fact]
    public void A_Process_Nudging_One_Core_Is_Low()
    {
        // ~2% of an 8-core machine.
        Assert.Equal(ProcessPowerBucket.Low, ProcessPowerScore.Classify(0.02, 0, 0));
    }

    [Fact]
    public void A_Process_Holding_A_Whole_Core_Is_High()
    {
        // One full core of eight.
        Assert.Equal(ProcessPowerBucket.High, ProcessPowerScore.Classify(0.125, 0, 0));
    }

    [Fact]
    public void A_Process_Pegging_The_Machine_Is_Very_High()
    {
        Assert.Equal(ProcessPowerBucket.VeryHigh, ProcessPowerScore.Classify(1.0, 0, 0));
    }

    [Fact]
    public void Sustained_Gpu_Work_Drives_The_Bucket_Up()
    {
        Assert.Equal(ProcessPowerBucket.VeryHigh, ProcessPowerScore.Classify(0, 0.4, 0));
    }

    [Fact]
    public void Heavy_Disk_Io_Alone_Is_Only_Moderate()
    {
        Assert.Equal(ProcessPowerBucket.Moderate, ProcessPowerScore.Classify(0, 0, 50_000_000));
    }

    [Fact]
    public void The_Bucket_Is_Monotonic_In_Cpu_Load()
    {
        ProcessPowerBucket previous = ProcessPowerBucket.VeryLow;

        foreach (double load in new[] { 0.0, 0.01, 0.05, 0.15, 0.4, 1.0 }) {
            ProcessPowerBucket bucket = ProcessPowerScore.Classify(load, 0, 0);
            Assert.True(bucket >= previous, $"{bucket} < {previous} at load {load}");
            previous = bucket;
        }
    }
}
