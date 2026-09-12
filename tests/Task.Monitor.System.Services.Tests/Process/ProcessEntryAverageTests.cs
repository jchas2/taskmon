using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed class ProcessEntryAverageTests
{
    [Fact]
    public void Add_Tracks_The_Running_Mean_And_The_Peak()
    {
        ProcessEntryAverage average = new();

        average.Add(new ProcessEntry { CpuTimePercent = 0.2, UsedMemory = 100, DiskBytesPerSecond = 1000 });
        average.Add(new ProcessEntry { CpuTimePercent = 0.8, UsedMemory = 300, DiskBytesPerSecond = 3000 });

        Assert.Equal(0.5, average.CpuTimePercent, precision: 6);
        Assert.Equal(200, average.UsedMemory);
        Assert.Equal(2000.0, average.DiskBytesPerSecond, precision: 6);

        Assert.Equal(0.8, average.CpuTimePercentMax, precision: 6);
        Assert.Equal(300, average.UsedMemoryMax);
        Assert.Equal(3000.0, average.DiskBytesPerSecondMax, precision: 6);
    }

    [Fact]
    public void Add_Keeps_The_Peak_After_The_Value_Falls_Away()
    {
        ProcessEntryAverage average = new();

        average.Add(new ProcessEntry { CpuTimePercent = 0.9 });
        average.Add(new ProcessEntry { CpuTimePercent = 0.0 });
        average.Add(new ProcessEntry { CpuTimePercent = 0.0 });

        Assert.Equal(0.9, average.CpuTimePercentMax, precision: 6);
        Assert.Equal(0.3, average.CpuTimePercent, precision: 6);
    }
}
