using Task.Monitor.Process;

namespace Task.Monitor.Tests.Process;

public sealed class ProcessAverageTests
{
    private const int Precision = 10;

    private static ProcessorInfo Sample(
        double cpu = 0.0,
        double gpu = 0.0,
        long memory = 0,
        long disk = 0) =>
        new() {
            CpuTimePercent = cpu,
            GpuTimePercent = gpu,
            UsedMemory = memory,
            DiskUsage = disk
        };

    [Fact]
    public void No_Samples_Returns_Zero()
    {
        ProcessAverage average = new();

        Assert.Equal(0.0, average.CpuTimePercent);
        Assert.Equal(0.0, average.GpuTimePercent);
        Assert.Equal(0, average.UsedMemory);
        Assert.Equal(0, average.DiskUsage);
    }

    [Fact]
    public void Single_Sample_Average_Equals_The_Sample()
    {
        ProcessAverage average = new();

        average.Add(Sample(cpu: 0.42, gpu: 0.17, memory: 123456, disk: 789));

        Assert.Equal(0.42, average.CpuTimePercent, Precision);
        Assert.Equal(0.17, average.GpuTimePercent, Precision);
        Assert.Equal(123456, average.UsedMemory);
        Assert.Equal(789, average.DiskUsage);
    }

    [Fact]
    public void Average_Of_Multiple_Samples_Equals_Arithmetic_Mean()
    {
        ProcessAverage average = new();

        average.Add(Sample(cpu: 0.1, gpu: 0.4, memory: 1000, disk: 10));
        average.Add(Sample(cpu: 0.2, gpu: 0.5, memory: 2000, disk: 20));
        average.Add(Sample(cpu: 0.3, gpu: 0.6, memory: 3000, disk: 30));

        Assert.Equal(0.2, average.CpuTimePercent, Precision);
        Assert.Equal(0.5, average.GpuTimePercent, Precision);
        Assert.Equal(2000, average.UsedMemory);
        Assert.Equal(20, average.DiskUsage);
    }

    [Fact]
    public void Running_Average_Tracks_The_Cumulative_Mean_After_Each_Sample()
    {
        ProcessAverage average = new();
        double[] samples = [5.0, 7.0, 1.0, 11.0, 6.0];

        double runningSum = 0.0;

        for (int i = 0; i < samples.Length; i++) {
            average.Add(Sample(cpu: samples[i]));
            runningSum += samples[i];

            double expectedMean = runningSum / (i + 1);

            Assert.Equal(expectedMean, average.CpuTimePercent, Precision);
        }
    }

    [Fact]
    public void Repeated_Identical_Samples_Preserve_The_Value()
    {
        ProcessAverage average = new();

        for (int i = 0; i < 1000; i++) {
            average.Add(Sample(cpu: 0.25, memory: 4096));
        }

        Assert.Equal(0.25, average.CpuTimePercent, Precision);
        Assert.Equal(4096, average.UsedMemory);
    }

    [Fact]
    public void Long_Metric_Means_Are_Truncated_Towards_Zero()
    {
        ProcessAverage average = new();

        average.Add(Sample(memory: 10, disk: 5));
        average.Add(Sample(memory: 11, disk: 8));

        Assert.Equal(10, average.UsedMemory);
        Assert.Equal(6, average.DiskUsage);
    }

    [Theory]
    [InlineData(new [] { 0.0 }, 0.0)]
    [InlineData(new [] { 1.0, 1.0, 1.0, 1.0 }, 1.0)]
    [InlineData(new [] { 0.0, 1.0 }, 0.5)]
    [InlineData(new [] { 0.1, 0.2, 0.3, 0.4 }, 0.25)]
    [InlineData(new [] { 2.0, 4.0, 6.0 }, 4.0)]
    public void Cpu_Average_Matches_Expected_Mean(double[] samples, double expected)
    {
        ProcessAverage average = new();

        foreach (double sample in samples) {
            average.Add(Sample(cpu: sample));
        }

        Assert.Equal(expected, average.CpuTimePercent, Precision);
    }
}
