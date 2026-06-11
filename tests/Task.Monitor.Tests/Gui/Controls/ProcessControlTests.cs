using Moq;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Process;
using Task.Monitor.System;
using Task.Monitor.Tests.Common;
using Task.Monitor.Tests.Process;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ProcessControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public ProcessControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        ProcessControl ctrl = new(
            runContext.Processor,
            runContext.Terminal, 
            runContext.AppConfig);

        Assert.NotNull(ctrl);
    }
    
    [Fact]
    public void Constructor_With_Null_Terminal_Throws_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => 
            new ProcessControl(
                runContext.Processor,
                null!,
                runContext.AppConfig));

    [Fact]
    public void Should_Draw_Control()
    {
        ProcessorFake processorFake = new();

        SystemStatistics statistics = new() {
            AvailablePhysical = 832244372,
            AvailablePageFile = 825226792,
            AvailableVirtual = 0,
            TotalPhysical = 38655765664,
            TotalPageFile = 2127482648,
            TotalVirtual = 0,
            CpuFrequency = 0,
            CpuCores = 12,
            CpuName = "Mac15,7",
            CpuPercentIdleTime = 0.7482,
            CpuPercentKernelTime = 0.1188,
            CpuPercentUserTime = 0.1333,
            GpuPercentTime = 0.1222,
            AvailableGpuMemory = 8279139243,
            TotalGpuMemory = 12351149720,
            MachineName = "mach01",
            OsVersion = "Unix 14.1.0",
            PublicIPv4Address = "",
            PrivateIPv4Address = "192.168.1.110",
            DiskUsage = 31047,
            ProcessCount = 742,
            ThreadCount = 4992,
        };

        List<ProcessorInfo> pinfos = new() {
            new() {
                BasePriority = 8,
                CmdLine = "//usr//bin//app1",
                CpuKernelTimePercent = 0.13871600000000001,
                CpuTimePercent = 0.20614333333333335,
                CpuUserTimePercent = 0.067427333333333339,
                GpuTimePercent = 0.1222,
                DiskUsage = 0,
                FileDescription = "app1",
                ThreadCount = 19,
                Pid = 45,
                UserName = "_user1",
                HandleCount = 0,
                IsDaemon = true,
                ParentPid = 0,
                ProcessName = "app1",
                StartTime = DateTime.Now,
                UsedMemory = 85000192,
                IsLowPriority = false,
            },
            new() {
                BasePriority = 8,
                CmdLine = "//usr//libexec//sysmond",
                CpuKernelTimePercent = 0.00097780000000000002,
                CpuTimePercent = 0.0034667333333333332,
                CpuUserTimePercent = 0.0024889333333333332,
                GpuTimePercent = 0.00,
                DiskUsage = 0,
                FileDescription = "sysmond",
                ThreadCount = 13,
                Pid = 431,
                UserName = "root",
                HandleCount = 0,
                IsDaemon = true,
                ParentPid = 0,
                ProcessName = "coreaudiod",
                StartTime = DateTime.Now,
                UsedMemory = 226911062,
                IsLowPriority = false,
            }
        };
        
        processorFake.AddSystemStats(statistics);
        processorFake.AddProcessorInfos(pinfos);
        
        ProcessControl ctrl = new(
            processorFake,
            runContext.Terminal, 
            runContext.AppConfig) {
            Width = 142,
            Height = 9
        };
        
        ctrl.Load();
        ctrl.Resize();
        processorFake.RaiseProcessorUpdatedEvent();

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("PROCESS"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("PID"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("USER"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("NI") || s.Contains("PRI"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("CPU%"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("THRDS"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("GPU%"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("MEM"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("DISK"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("PATH"))), Times.Once);

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("app1"))), Times.Exactly(2));
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("45"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("_user1"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("8 "))), Times.Exactly(2));
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("20.61%"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("19"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("12.22%"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("81.1 MB"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("0.0 MB/s"))), Times.Exactly(2));
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("//usr//bin//app1"))), Times.Once);

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("sysmond"))), Times.Exactly(2));
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("431"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("root"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("8 "))), Times.Exactly(2));
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("00.35%"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("13"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("00.00%"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("216.4 MB"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("0.0 MB/s"))), Times.Exactly(2));
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("//usr//libexec//sysmond"))), Times.Once);

        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }
}