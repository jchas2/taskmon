using System.Reflection;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Process;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

// VisibleColumnsOverride lets a ProcessControl hosted outside the PROCESSES screen (e.g. a
// SummaryControl2 pane) show its own nominated column subset independent of the app-wide
// AppConfig.VisibleColumns setting - these tests confirm two instances against the same AppConfig
// render different columns purely from their own override.
public sealed class ProcessControlColumnOverrideTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public ProcessControlColumnOverrideTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    // Reflection is the only way to raise this from a test: ServiceController only ever raises it
    // itself, from inside its real worker loop.
    private static void RaiseSnapshotUpdated(ServiceController serviceController, SystemSnapshot snapshot)
    {
        FieldInfo? field = typeof(ServiceController).GetField(
            "SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance);

        MulticastDelegate? handler = (MulticastDelegate?)field!.GetValue(serviceController);
        handler?.DynamicInvoke(serviceController, new SystemSnapshotEventArgs(snapshot));
    }

    private static SystemSnapshot BuildSnapshot() =>
        new() {
            Processes = new ProcessInfo {
                Metrics = new ProcessMetrics {
                    Entries = [
                        new ProcessEntry {
                            Pid = 1111,
                            ProcessName = "proc1111",
                            FileDescription = "Process 1111",
                            CpuTimePercent = 0.5,
                        },
                    ],
                },
            },
        };

    private ProcessControl CreateControl(Statistics? visibleColumnsOverride)
    {
        ProcessControl ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            VisibleColumnsOverride = visibleColumnsOverride,
            Width = 120,
            Height = 20
        };

        ctrl.Load();
        ctrl.Resize();

        return ctrl;
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Shows_Only_The_Overrides_Own_Columns_Not_The_AppConfig_Wide_Setting()
    {
        runContext.AppConfig.VisibleColumns = Statistics.Mem;

        ProcessControl ctrl = CreateControl(Statistics.Cpu);

        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot());
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("CPU%", output);
        Assert.DoesNotContain("MEM", output);

        ctrl.Unload();
    }

    [Fact]
    public void Two_Instances_Against_The_Same_AppConfig_Render_Different_Columns()
    {
        runContext.AppConfig.VisibleColumns = Statistics.Cpu | Statistics.Mem;

        ProcessControl cpuOnly = CreateControl(Statistics.Cpu);
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot());
        cpuOnly.Draw();
        string cpuOnlyOutput = CapturedOutput();
        cpuOnly.Unload();

        runContextHelper.terminal.Invocations.Clear();

        ProcessControl memOnly = CreateControl(Statistics.Mem);
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot());
        memOnly.Draw();
        string memOnlyOutput = CapturedOutput();
        memOnly.Unload();

        Assert.Contains("CPU%", cpuOnlyOutput);
        Assert.DoesNotContain("MEM", cpuOnlyOutput);

        Assert.Contains("MEM", memOnlyOutput);
        Assert.DoesNotContain("CPU%", memOnlyOutput);
    }

    [Fact]
    public void Null_Override_Falls_Back_To_The_AppConfig_Wide_Setting()
    {
        runContext.AppConfig.VisibleColumns = Statistics.Mem;

        ProcessControl ctrl = CreateControl(visibleColumnsOverride: null);

        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot());
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("MEM", output);
        Assert.DoesNotContain("CPU%", output);

        ctrl.Unload();
    }
}
