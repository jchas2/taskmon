using System.Collections.Immutable;
using Moq;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class CpuPerformanceControlTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public CpuPerformanceControlTests()
    {
        runContextHelper = new RunContextHelper();
        runContext       = runContextHelper.GetRunContext();
    }

    private static CpuInfo BuildCpuInfo() =>
        new() {
            Specs       = new CpuSpecs { CpuName = "Test CPU" },
            CoreMetrics = Enumerable.Range(0, Environment.ProcessorCount)
                .Select(index => new CpuInfo.CpuCoreMetric(index.ToString(), 0.5))
                .ToImmutableArray()
        };

    private CpuPerformanceControl CreateControl()
    {
        // Wrap the mock terminal so the Chart's ReadOnlySpan<char> blit doesn't hit the Moq
        // proxy (which can't proxy ref-struct params); other writes still forward to the mock.
        CpuPerformanceControl ctrl = new(new ForwardingTerminal(runContext.Terminal), runContext.AppConfig) {
            Width  = 120,
            Height = 40
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    // Regression test: the Tasks and Threads cells were never filled in and always showed 0.
    [Fact]
    public void Draws_The_Task_And_Thread_Counts()
    {
        CpuPerformanceControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot {
            Cpu       = BuildCpuInfo(),
            Processes = new ProcessInfo { Metrics = new ProcessMetrics { ProcessCount = 312, ThreadCount = 4567 } }
        });
        ctrl.Draw();

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("312"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("4567"))), Times.AtLeastOnce);

        ctrl.Unload();
    }

    [Fact]
    public void Keeps_The_Last_Counts_When_A_Snapshot_Has_None()
    {
        CpuPerformanceControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot {
            Cpu       = BuildCpuInfo(),
            Processes = new ProcessInfo { Metrics = new ProcessMetrics { ProcessCount = 312, ThreadCount = 4567 } }
        });
        ctrl.Sample(new SystemSnapshot { Cpu = BuildCpuInfo() });

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("4567"))), Times.AtLeastOnce);

        ctrl.Unload();
    }

    [Fact]
    public void Names_The_Process_Count_Tasks_As_The_Header_Does()
    {
        CpuPerformanceControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot { Cpu = BuildCpuInfo() });
        ctrl.Draw();

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Tasks"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Processes"))), Times.Never);

        ctrl.Unload();
    }
}
