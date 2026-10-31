using System.Reflection;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ProcessControlEndTaskTests : IDisposable
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public ProcessControlEndTaskTests()
    {
        runContextHelper = new RunContextHelper();
        runContext       = runContextHelper.GetRunContext();
    }

    private static SystemSnapshot BuildSnapshot(params int[] pids) =>
        new() {
            Processes = new ProcessInfo {
                Metrics = new ProcessMetrics {
                    Entries = pids.Select((pid, i) => new ProcessEntry {
                        Pid             = pid,
                        ProcessName     = $"proc{pid}",
                        FileDescription = $"Process {pid}",
                        CpuTimePercent  = 1.0 - i * 0.1,
                    }).ToList(),
                },
            },
        };

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Confirmation_Lists_Three_Targets_And_A_Count_Of_The_Rest()
    {
        runContext.AppConfig.MultiSelectProcesses = true;
        ProcessControl ctrl = CreateControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222, 3333, 4444, 5555));
        ctrl.EndTask = _ => { };

        for (int i = 0; i < 5; i++) {
            Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.Spacebar));
            Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow));
        }

        runContextHelper.terminal.Invocations.Clear();
        Press(ctrl, EndTaskKey);

        string output = CapturedOutput();
        Assert.Contains("End Tasks", output);
        Assert.Contains("End these 5 tasks?", output);
        Assert.Contains("(1111)", output);
        Assert.Contains("and 2 more", output);
        Assert.Contains("Child processes are also ended.", output);

        ctrl.Unload();
    }

    [Fact]
    public void Confirming_Closes_The_Dialog_And_Shows_No_Further_Message()
    {
        ProcessControl ctrl = CreateControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));
        ctrl.EndTask = _ => { };

        Press(ctrl, EndTaskKey);
        Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter));

        Assert.False(ctrl.IsEndTaskDialogVisible);
        Assert.False(ctrl.IsModalActive);
        Assert.True(Control.RedrawEnabled);

        ctrl.Unload();
    }

    private ProcessControl CreateControl(int width = 120)
    {
        ProcessControl ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width  = width,
            Height = 40
        };

        ctrl.Load();
        ctrl.Resize();

        return ctrl;
    }

    public void Dispose() => Control.RedrawEnabled = true;

    private static ConsoleKeyInfo EndTaskKey => new('k', ConsoleKey.K, false, false, false);

    [Fact]
    public void Escape_In_The_Confirmation_Ends_Nothing()
    {
        ProcessControl ctrl = CreateControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));
        List<int> ended = [];
        ctrl.EndTask = ended.Add;

        Press(ctrl, EndTaskKey);
        Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.Escape));

        Assert.Empty(ended);
        Assert.False(ctrl.IsEndTaskDialogVisible);
        Assert.True(Control.RedrawEnabled);

        ctrl.Unload();
    }

    [Fact]
    public void Footer_Shows_The_End_Task_Hint()
    {
        ProcessControl ctrl = CreateControl(width: 160);
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));

        Assert.Contains("k End Task   f Filter", AnsiTestHelper.StripColours(CapturedOutput()));

        ctrl.Unload();
    }

    [Fact]
    public void K_Asks_For_Confirmation_And_Enter_Ends_The_Selected_Task()
    {
        ProcessControl ctrl = CreateControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));
        List<int> ended = [];
        ctrl.EndTask = ended.Add;

        Assert.True(Press(ctrl, EndTaskKey));

        Assert.True(ctrl.IsEndTaskDialogVisible);
        Assert.False(Control.RedrawEnabled);
        Assert.Contains("End this task?", CapturedOutput());
        Assert.Empty(ended);

        Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter));

        Assert.Equal([1111], ended);

        ctrl.Unload();
    }

    [Fact]
    public void K_Ends_Every_Checked_Process_When_Multi_Select_Is_On()
    {
        runContext.AppConfig.MultiSelectProcesses = true;
        ProcessControl ctrl = CreateControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222, 3333));
        List<int> ended = [];
        ctrl.EndTask = ended.Add;

        Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.Spacebar));
        Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow));
        Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow));
        Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.Spacebar));

        Press(ctrl, EndTaskKey);
        Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter));

        Assert.Equal([1111, 3333], ended);

        ctrl.Unload();
    }

    [Fact]
    public void K_Ends_Only_The_Selected_Process_When_Nothing_Is_Checked()
    {
        runContext.AppConfig.MultiSelectProcesses = true;
        ProcessControl ctrl = CreateControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222, 3333));
        List<int> ended = [];
        ctrl.EndTask = ended.Add;

        Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow));
        Press(ctrl, EndTaskKey);
        Press(ctrl, ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter));

        Assert.Equal([2222], ended);

        ctrl.Unload();
    }

    [Fact]
    public void K_Never_Ends_Task_Monitor_Itself()
    {
        ProcessControl ctrl = CreateControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(Environment.ProcessId));
        List<int> ended = [];
        ctrl.EndTask = ended.Add;

        Assert.True(Press(ctrl, EndTaskKey));

        Assert.False(ctrl.IsEndTaskDialogVisible);
        Assert.Empty(ended);

        ctrl.Unload();
    }

    [Fact]
    public void K_Without_Confirmation_Ends_Immediately()
    {
        runContext.AppConfig.ConfirmTaskDelete = false;
        ProcessControl ctrl = CreateControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));
        List<int> ended = [];
        ctrl.EndTask = ended.Add;

        Press(ctrl, EndTaskKey);

        Assert.Equal([1111], ended);
        Assert.False(ctrl.IsEndTaskDialogVisible);
        Assert.True(Control.RedrawEnabled);

        ctrl.Unload();
    }

    [Fact]
    public void K_With_An_Empty_List_Does_Nothing()
    {
        ProcessControl ctrl = CreateControl();
        List<int> ended = [];
        ctrl.EndTask = ended.Add;

        Press(ctrl, EndTaskKey);

        Assert.False(ctrl.IsEndTaskDialogVisible);
        Assert.Empty(ended);

        ctrl.Unload();
    }

    private static bool Press(ProcessControl ctrl, ConsoleKeyInfo keyInfo)
    {
        bool handled = false;
        ctrl.KeyPressed(keyInfo, ref handled);
        return handled;
    }

    private static void RaiseSnapshotUpdated(ServiceController serviceController, SystemSnapshot snapshot)
    {
        FieldInfo? field = typeof(ServiceController).GetField(
            "SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance);

        MulticastDelegate? handler = (MulticastDelegate?)field!.GetValue(serviceController);
        handler?.DynamicInvoke(serviceController, new SystemSnapshotEventArgs(snapshot));
    }
}
