using Moq;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Task.Monitor.Tests.Process;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class HeaderControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public HeaderControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        HeaderControl ctrl = new(
            runContext.Processor,
            runContext.Terminal, 
            runContext.AppConfig);

        Assert.NotNull(ctrl);
    }
    
    [Fact]
    public void Constructor_With_Null_Terminal_Throws_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => 
            new HeaderControl(
                runContext.Processor,
                null!,
                runContext.AppConfig));

    [Fact]
    public void Should_Draw_Header()
    {
        ProcessorFake processorFake = new();

        SystemStatistics statistics = new() {
            AvailablePhysical = 935247872,
            AvailablePageFile = 815726592,
            AvailableVirtual = 0,
            TotalPhysical = 38654705664,
            TotalPageFile = 2147483648,
            TotalVirtual = 0,
            CpuFrequency = 0,
            CpuCores = 12,
            CpuName = "Mac15,7",
            CpuPercentIdleTime = 0.7482,
            CpuPercentKernelTime = 0.1188,
            CpuPercentUserTime = 0.1333,
            GpuCores = 18,
            GpuPercentTime = 0.1232,
            MachineName = "mach01",
            OsVersion = "Unix 14.1.0",
            PublicIPv4Address = "",
            PrivateIPv4Address = "192.168.1.110",
            TotalDiskReadBytes = 98324528934,
            TotalDiskWriteBytes = 21983484324,
            DiskUsage = 30037,
            TotalNetworkBytesReceived = 89213742,
            TotalNetworkBytesSent = 298346234,
            TotalNetworkPacketsReceived = 763442,
            TotalNetworkPacketsSent = 346723,
            ProcessCount = 739,
            ThreadCount = 4993,
            RunningCount = 102
        };

        processorFake.AddSystemStats(statistics);
        
        // Wrap the mock terminal so the Chart's ReadOnlySpan<char> blit doesn't hit the Moq
        // proxy (which can't proxy ref-struct params); other writes still forward to the mock.
        HeaderControl ctrl = new(
            processorFake,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 256,
            Height = 32
        };

        ctrl.Load();
        ctrl.Resize();
        processorFake.RaiseProcessorUpdatedEvent();
        
        Assert.True(runContext.AppConfig.MetreStyle == MetreControlStyle.Dots);
        
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("TASK MONITOR"))), Times.Once);

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("mach01"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Unix 14.1.0"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("192.168.1.110"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Mac15,7"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("12 Cores"))), Times.Once);

        if (OperatingSystem.IsMacOS()) {
            runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("18 Gpu"))), Times.Once);
        }

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Tasks:"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("739"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Threads:"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("4993"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("102"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("running"))), Times.Once);
        
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Cpu"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Memory"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Swap") || s.Contains("Virtual"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Disk"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Net Sent"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Net Rec"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Gpu"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Gpu Memory"))), Times.Once);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }
}
