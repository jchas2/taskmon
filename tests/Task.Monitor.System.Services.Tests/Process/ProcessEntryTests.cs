using Task.Monitor.System.Services.Process;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed class ProcessEntryTests
{
    [Fact]
    public void ProcessEntry_Canary_Test() =>
        Assert.Equal(30, CanaryTestHelper.GetPropertyCount<ProcessEntry>());

    [Fact]
    public void Default_Constructor_Initializes_With_Default_Values()
    {
        ProcessEntry entry = new();

        Assert.Equal(0, entry.Pid);
        Assert.Equal(0, entry.ParentPid);
        Assert.Equal(0, entry.ThreadCount);
        Assert.Equal(0U, entry.HandleCount);
        Assert.Equal(0, entry.BasePriority);
        Assert.False(entry.IsDaemon);
        Assert.False(entry.IsLowPriority);
        Assert.False(entry.IsRunningAsRoot);
        Assert.Equal(string.Empty, entry.ProcessName);
        Assert.Equal(string.Empty, entry.FileDescription);
        Assert.Equal(string.Empty, entry.UserName);
        Assert.Equal(string.Empty, entry.CmdLine);
        Assert.Equal(0, entry.UsedMemory);
        Assert.Equal(0, entry.CommittedMemory);
        Assert.Equal(0UL, entry.DiskReadBytes);
        Assert.Equal(0UL, entry.DiskWriteBytes);
        Assert.Equal(0.0, entry.DiskBytesPerSecond);
        Assert.Equal(0.0, entry.CpuTimePercent);
        Assert.Equal(0.0, entry.CpuUserTimePercent);
        Assert.Equal(0.0, entry.CpuKernelTimePercent);
        Assert.Equal(0.0, entry.GpuTimePercent);
        Assert.Equal(0.0, entry.CpuTimePercentAvg);
        Assert.Equal(0.0, entry.GpuTimePercentAvg);
        Assert.Equal(0, entry.UsedMemoryAvg);
        Assert.Equal(0.0, entry.DiskBytesPerSecondAvg);
        Assert.Equal(0.0, entry.CpuTimePercentMax);
        Assert.Equal(0.0, entry.GpuTimePercentMax);
        Assert.Equal(0, entry.UsedMemoryMax);
        Assert.Equal(0.0, entry.DiskBytesPerSecondMax);
        Assert.Equal(ProcessPowerBucket.VeryLow, entry.PowerBucket);
    }

    [Fact]
    public void Properties_Can_Be_Set_And_Retrieved()
    {
        ProcessEntry entry = new() {
            Pid                   = 1234,
            ParentPid             = 1,
            ThreadCount           = 10,
            HandleCount           = 50,
            BasePriority          = 8,
            IsDaemon              = true,
            IsLowPriority         = true,
            IsRunningAsRoot       = true,
            ProcessName           = "test.exe",
            FileDescription       = "Test Process",
            UserName              = "testuser",
            CmdLine               = "/usr/bin/test --arg",
            UsedMemory            = 2048,
            CommittedMemory       = 3072,
            DiskReadBytes         = 999888,
            DiskWriteBytes        = 222333,
            DiskBytesPerSecond    = 1024.5,
            CpuTimePercent        = 15.5,
            CpuUserTimePercent    = 10.2,
            CpuKernelTimePercent  = 5.3,
            GpuTimePercent        = 4.1,
            CpuTimePercentAvg     = 12.7,
            GpuTimePercentAvg     = 3.6,
            UsedMemoryAvg         = 4096,
            DiskBytesPerSecondAvg = 512.25,
            CpuTimePercentMax     = 33.3,
            GpuTimePercentMax     = 9.9,
            UsedMemoryMax         = 8192,
            DiskBytesPerSecondMax = 2048.75,
            PowerBucket           = ProcessPowerBucket.VeryHigh
        };

        Assert.Equal(1234, entry.Pid);
        Assert.Equal(1, entry.ParentPid);
        Assert.Equal(10, entry.ThreadCount);
        Assert.Equal(50U, entry.HandleCount);
        Assert.Equal(8, entry.BasePriority);
        Assert.True(entry.IsDaemon);
        Assert.True(entry.IsLowPriority);
        Assert.True(entry.IsRunningAsRoot);
        Assert.Equal("test.exe", entry.ProcessName);
        Assert.Equal("Test Process", entry.FileDescription);
        Assert.Equal("testuser", entry.UserName);
        Assert.Equal("/usr/bin/test --arg", entry.CmdLine);
        Assert.Equal(2048, entry.UsedMemory);
        Assert.Equal(3072, entry.CommittedMemory);
        Assert.Equal(999888UL, entry.DiskReadBytes);
        Assert.Equal(222333UL, entry.DiskWriteBytes);
        Assert.Equal(1024.5, entry.DiskBytesPerSecond);
        Assert.Equal(15.5, entry.CpuTimePercent);
        Assert.Equal(10.2, entry.CpuUserTimePercent);
        Assert.Equal(5.3, entry.CpuKernelTimePercent);
        Assert.Equal(4.1, entry.GpuTimePercent);
        Assert.Equal(12.7, entry.CpuTimePercentAvg);
        Assert.Equal(3.6, entry.GpuTimePercentAvg);
        Assert.Equal(4096, entry.UsedMemoryAvg);
        Assert.Equal(512.25, entry.DiskBytesPerSecondAvg);
        Assert.Equal(33.3, entry.CpuTimePercentMax);
        Assert.Equal(9.9, entry.GpuTimePercentMax);
        Assert.Equal(8192, entry.UsedMemoryMax);
        Assert.Equal(2048.75, entry.DiskBytesPerSecondMax);
        Assert.Equal(ProcessPowerBucket.VeryHigh, entry.PowerBucket);
    }
}
