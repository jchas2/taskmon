using System.Globalization;
using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.Process;
using Task.Monitor.System;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ProcessListViewItemTests
{
    private readonly AppConfig appConfig;
    private readonly Mock<IFileSystem> fileSystem = new();
    
    public ProcessListViewItemTests() =>
        appConfig = new AppConfig(fileSystem.Object);

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_SubItems_Successfully()
    {
        ProcessorInfo processorInfo = new() {
            Pid = 1234,
            FileDescription = "test.exe",
            UserName = "test",
            BasePriority = 8,
            CpuTimePercent = 0.055,
            ThreadCount = 4,
            UsedMemory = 1024 * 1024,
            DiskUsage = 0,
            CmdLine = "/usr/bin/test"
        };

        SystemStatistics stats = new() {
            TotalPhysical = 1024 * 1024 * 1024
        };

        ProcessControl.ProcessListViewItem item = new(
            processorInfo, 
            ref stats, 
            appConfig);

        Assert.NotNull(item);
        Assert.Equal(1234, item.Pid);
        
        Assert.Equal("test.exe", item.SubItems[(int)ProcessControl.Columns.Process].Text);
        Assert.Equal("1234", item.SubItems[(int)ProcessControl.Columns.Pid].Text);
        Assert.Equal("test", item.SubItems[(int)ProcessControl.Columns.User].Text);
        Assert.Equal("8", item.SubItems[(int)ProcessControl.Columns.Priority].Text);
        Assert.Equal("05.50%", item.SubItems[(int)ProcessControl.Columns.Cpu].Text);
        Assert.Equal("4", item.SubItems[(int)ProcessControl.Columns.Threads].Text);
    }
    
    public static TheoryData<int, string, string> ProcessorInfoData()
        => new() {
            { 100, "system", "System Process" },
            { 200, "admin", "Admin Tool" },
            { 300, "root", "Root Service" },
            { 999, "guest", "Guest Application" }
        };

    [Theory]
    [MemberData(nameof(ProcessorInfoData))]
    public void Constructor_With_Various_ProcessorInfo_Sets_Properties_Correctly(
        int pid,
        string userName,
        string fileDescription)
    {
        ProcessorInfo processorInfo = new() {
            Pid = pid,
            FileDescription = fileDescription,
            UserName = userName
        };

        SystemStatistics stats = new() {
            TotalPhysical = 1024 * 1024 * 1024
        };

        ProcessControl.ProcessListViewItem item = new(
            processorInfo, 
            ref stats, 
            appConfig);

        Assert.Equal(pid, item.Pid);
        Assert.Equal(userName, item.SubItems[(int)ProcessControl.Columns.User].Text);
        Assert.Equal(fileDescription, item.Text);
    }

    [Fact]
    public void UpdateSubItems_Updates_Text_Values()
    {
        ProcessorInfo initialInfo = new() {
            Pid = 1234,
            FileDescription = "initial.exe",
            UserName = "user",
            BasePriority = 8,
            CpuTimePercent = 0.022,
            ThreadCount = 4,
            UsedMemory = 1024 * 1024,
            DiskUsage = 100,
            CmdLine = "/initial"
        };

        SystemStatistics stats = new() {
            TotalPhysical = 1024 * 1024 * 1024
        };

        ProcessControl.ProcessListViewItem item = new(
            initialInfo, 
            ref stats, 
            appConfig);

        ProcessorInfo updatedInfo = new() {
            Pid = 1234,
            FileDescription = "updated.exe",
            UserName = "newuser",
            BasePriority = 10,
            CpuTimePercent = 0.155,
            ThreadCount = 8,
            UsedMemory = 2048 * 1024,
            DiskUsage = 500,
            CmdLine = "/updated"
        };

        item.UpdateSubItems(updatedInfo, ref stats);

        Assert.Equal("updated.exe", item.SubItems[(int)ProcessControl.Columns.Process].Text);
        Assert.Equal("1234", item.SubItems[(int)ProcessControl.Columns.Pid].Text);
        Assert.Equal("newuser", item.SubItems[(int)ProcessControl.Columns.User].Text);
        Assert.Equal("10", item.SubItems[(int)ProcessControl.Columns.Priority].Text);
        Assert.Equal("15.50%", item.SubItems[(int)ProcessControl.Columns.Cpu].Text);
        Assert.Equal("8", item.SubItems[(int)ProcessControl.Columns.Threads].Text);
    }

    public static TheoryData<double, ulong, bool> MemoryUsageData()
        => new()
        {
            { 0.05, 1024UL * 1024 * 1024, false },  // 5% - no highlighting
            { 0.15, 1024UL * 1024 * 1024, true },   // 15% - low range
            { 0.35, 1024UL * 1024 * 1024, true },   // 35% - mid range
            { 0.75, 1024UL * 1024 * 1024, true }    // 75% - high range
        };

    [Theory]
    [MemberData(nameof(MemoryUsageData))]
    public void Constructor_With_Various_Memory_Usage_Applies_Formatting(
        double memoryRatio,
        ulong totalPhysical,
        bool expectsFormatting)
    {
        long usedMemory = (long)(memoryRatio * totalPhysical);

        ProcessorInfo processorInfo = new() {
            Pid = 1234,
            FileDescription = "test.exe",
            UsedMemory = usedMemory
        };

        SystemStatistics stats = new() {
            TotalPhysical = totalPhysical
        };

        ProcessControl.ProcessListViewItem item = new(processorInfo, ref stats, appConfig);

        if (expectsFormatting) {
            // When memory ratio > 10%, background color should be different from default
            Assert.NotEqual(
                appConfig.DefaultTheme.Background,
                item.SubItems[(int)ProcessControl.Columns.Memory].BackgroundColor);
        }
    }

    public static TheoryData<double> CpuUsageData()
        => new() {
            0.5,
            5.0,
            25.0,
            50.0,
            75.0,
            99.9
        };

    [Theory]
    [MemberData(nameof(CpuUsageData))]
    public void Constructor_With_Various_Cpu_Values_Formats_Correctly(double cpuPercent)
    {
        ProcessorInfo processorInfo = new()
        {
            Pid = 1234,
            FileDescription = "test.exe",
            CpuTimePercent = cpuPercent
        };

        SystemStatistics stats = new() {
            TotalPhysical = 1024 * 1024 * 1024
        };

        ProcessControl.ProcessListViewItem item = new(
            processorInfo, 
            ref stats, 
            appConfig);

        string expectedCpuText = cpuPercent.ToString("00.00%", CultureInfo.InvariantCulture);
        Assert.Equal(expectedCpuText, item.SubItems[(int)ProcessControl.Columns.Cpu].Text);
    }

    public static TheoryData<long, string> DiskUsageData()
        => new()
        {
            { 0, "  0.0 MB/s" },
            { 1024, "  0.1 MB/s" },
            { 1024 * 1024, "  1.1 MB/s" },
            { 10 * 1024 * 1024, " 10.5 MB/s" },
            { 100 * 1024 * 1024, "104.9 MB/s" }
        };

    [Theory]
    [MemberData(nameof(DiskUsageData))]
    public void Constructor_With_Various_Disk_Usage_Creates_SubItem(long diskUsage, string expected)
    {
        ProcessorInfo processorInfo = new() {
            Pid = 1234,
            FileDescription = "test.exe",
            DiskUsage = diskUsage
        };

        SystemStatistics stats = new() {
            TotalPhysical = 1024 * 1024 * 1024
        };

        ProcessControl.ProcessListViewItem item = new(processorInfo, ref stats, appConfig);

        Assert.NotNull(item.SubItems[(int)ProcessControl.Columns.Disk].Text);
        Assert.Equal(expected, item.SubItems[(int)ProcessControl.Columns.Disk].Text);
    }
}
