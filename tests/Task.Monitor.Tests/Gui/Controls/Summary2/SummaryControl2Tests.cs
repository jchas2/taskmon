using System.Reflection;
using Task.Monitor.Gui.Controls.Summary2;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

public sealed class SummaryControl2Tests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public SummaryControl2Tests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        SummaryControl2 ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    // Reflection is the only way to raise this from a test: ServiceController only ever raises it
    // itself, from inside its real worker loop.
    private static void RaiseSnapshotUpdated(ServiceController serviceController, SystemSnapshot snapshot)
    {
        FieldInfo? field = typeof(ServiceController).GetField(
            "SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance);

        MulticastDelegate? handler = (MulticastDelegate?)field!.GetValue(serviceController);
        handler?.DynamicInvoke(serviceController, new SystemSnapshotEventArgs(snapshot));
    }

    [Fact]
    public void Draws_The_Example_Trees_Chart_Titles_And_Process_Header()
    {
        SummaryControl2 ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = 120,
            Height = 40
        };

        ctrl.Load();
        ctrl.Resize();
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("Cpu", output);
        Assert.Contains("Memory", output);
        Assert.Contains("Gpu", output);
        Assert.Contains("Top", output);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Feeds_A_Published_Snapshot_Into_The_Cpu_Chart()
    {
        SummaryControl2 ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = 120,
            Height = 40
        };

        ctrl.Load();
        ctrl.Resize();

        runContext.AppConfig.ShowMetreCpuNumerically = true;

        RaiseSnapshotUpdated(runContext.ServiceController, new SystemSnapshot {
            Cpu = new CpuInfo {
                Metrics = new CpuMetrics {
                    CpuPercentKernelTime = 0.1,
                    CpuPercentUserTime = 0.2,
                }
            }
        });

        ctrl.Draw();

        Assert.Contains("Kernel", CapturedOutput());

        ctrl.Unload();
    }

    // Regression test: panes are loaded by base.OnLoad() (Control.OnLoad loads every child), so an
    // extra explicit Load() loop here double-subscribed ProcessControl to snapshot updates -
    // rows redrawn twice per tick, and an Unload() that left one live subscription behind.
    [Fact]
    public void Each_Pane_Is_Subscribed_To_Snapshot_Updates_Exactly_Once()
    {
        SummaryControl2 ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = 120,
            Height = 40
        };

        ctrl.Load();

        MulticastDelegate? handler = (MulticastDelegate?)typeof(ServiceController)
            .GetField("SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(runContext.ServiceController);

        Assert.Equal(1, handler!.GetInvocationList().Count(d => d.Target?.GetType().Name == "ProcessControl"));

        ctrl.Unload();

        handler = (MulticastDelegate?)typeof(ServiceController)
            .GetField("SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(runContext.ServiceController);

        Assert.Equal(0, handler?.GetInvocationList().Count(d => d.Target?.GetType().Name == "ProcessControl") ?? 0);
    }

    // The SUMMARY screen's layout is chosen in Setup (or edited in the designer) while this control
    // already exists - it has to pick the change up the next time it's shown (Unload + Load, which
    // is what MainScreen2 does when it comes back to the front).
    [Fact]
    public void A_Changed_Default_Layout_Is_Picked_Up_On_The_Next_Load()
    {
        SummaryControl2 ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = 120,
            Height = 40
        };

        ctrl.Load();
        Assert.Equal(9, ctrl.Controls.Count); // All Charts: 8 charts + the process list
        ctrl.Unload();

        runContext.AppConfig.DefaultSummaryLayout2 =
            runContext.AppConfig.SummaryLayouts2.Single(l => l.Name == "Cpu and Memory");

        ctrl.Load();
        Assert.Equal(3, ctrl.Controls.Count); // Cpu, Memory, the process list

        // The new panes are live - exactly one ProcessControl subscription, none left from before.
        MulticastDelegate? handler = (MulticastDelegate?)typeof(ServiceController)
            .GetField("SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(runContext.ServiceController);

        Assert.Equal(1, handler!.GetInvocationList().Count(d => d.Target?.GetType().Name == "ProcessControl"));

        ctrl.Unload();
    }

    [Fact]
    public void An_Unchanged_Default_Keeps_The_Same_Pane_Controls_Across_Reloads()
    {
        SummaryControl2 ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig);

        ctrl.Load();
        List<Control> before = ctrl.Controls.ToList();
        ctrl.Unload();

        ctrl.Load();
        Assert.Equal(before, ctrl.Controls.ToList());

        ctrl.Unload();
    }

    // A Process pane's rows come from ProcessControl's own snapshot subscription, wired in its
    // OnLoad - which runs via SummaryControl2's base.OnLoad() (Control.OnLoad loads every child).
    [Fact]
    public void Process_Pane_Populates_Rows_From_A_Published_Snapshot()
    {
        SummaryControl2 ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = 120,
            Height = 40
        };

        ctrl.Load();
        ctrl.Resize();

        RaiseSnapshotUpdated(runContext.ServiceController, new SystemSnapshot {
            Processes = new ProcessInfo {
                Metrics = new ProcessMetrics {
                    Entries = [
                        new ProcessEntry {
                            Pid = 4242,
                            ProcessName = "proc4242",
                            FileDescription = "Distinctive Test Process",
                            CpuTimePercent = 0.5,
                        },
                    ],
                },
            },
        });

        ctrl.Draw();

        Assert.Contains("Distinctive Test Process", CapturedOutput());

        ctrl.Unload();
    }

    // Regression coverage for the same OnGotFocus redirect bug this session hit repeatedly:
    // SummaryControl2 itself never holds Focused = true (it always redirects to whichever pane
    // was last focused), so callers must ask HasFocus, not Focused.
    [Fact]
    public void Focus_Redirects_To_The_First_Pane_In_Tree_Order()
    {
        ForwardingTerminal terminal = new(runContext.Terminal);
        Screen screen = new(terminal) { Width = 120, Height = 40 };

        SummaryControl2 ctrl = new(runContext.ServiceController, terminal, runContext.AppConfig) {
            Width = 120,
            Height = 40
        };

        screen.Controls.Add(ctrl);
        ctrl.Load();
        ctrl.Resize();
        ctrl.SetFocus();

        Assert.False(ctrl.Focused);

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false), ref handled);

        Assert.True(handled);

        ctrl.Unload();
    }

    // Regression test: only Left/Right should cycle between panes. Up/Down go to the focused
    // pane's own handling (e.g. a Process pane's list scrolling) and must never move focus to
    // another pane, and Right must reach every pane - including stepping down out of the chart
    // row into the Process pane underneath it, which a spatial (rather than tree-order) search
    // previously failed to do.
    [Fact]
    public void Right_Arrow_Cycles_Through_Every_Pane_In_Tree_Order_And_Then_Stops()
    {
        // The pane count below is the built-in example tree's (Cpu, Memory, Gpu, Process) - clear
        // the configured default so that's the tree loaded, not the shipped 9-pane "All Charts".
        runContext.AppConfig.DefaultSummaryLayout2 = null;

        ForwardingTerminal terminal = new(runContext.Terminal);
        Screen screen = new(terminal) { Width = 120, Height = 40 };

        SummaryControl2 ctrl = new(runContext.ServiceController, terminal, runContext.AppConfig) {
            Width = 120,
            Height = 40
        };

        screen.Controls.Add(ctrl);
        ctrl.Load();
        ctrl.Resize();
        ctrl.SetFocus();

        ConsoleKeyInfo right = new('\0', ConsoleKey.RightArrow, false, false, false);
        bool handled;

        // Cpu -> Memory -> Gpu -> Process: three Right presses reach the Process pane, the fourth
        // has nowhere further to go and must be left unhandled rather than looping or getting stuck.
        for (int i = 0; i < 3; i++) {
            handled = false;
            ctrl.KeyPressed(right, ref handled);
            Assert.True(handled);
        }

        handled = false;
        ctrl.KeyPressed(right, ref handled);
        Assert.False(handled);

        ctrl.Unload();
    }

    // Regression test: Up/Down must never move focus between panes - only Left/Right do.
    [Fact]
    public void Up_And_Down_Arrows_Never_Move_Focus_Between_Panes()
    {
        ForwardingTerminal terminal = new(runContext.Terminal);
        Screen screen = new(terminal) { Width = 120, Height = 40 };

        SummaryControl2 ctrl = new(runContext.ServiceController, terminal, runContext.AppConfig) {
            Width = 120,
            Height = 40
        };

        screen.Controls.Add(ctrl);
        ctrl.Load();
        ctrl.Resize();
        ctrl.SetFocus();

        // Move onto the Process pane, which does have its own internal use for Down (scrolling),
        // so it is the sharpest case for confirming Down never bubbles into pane-switching.
        bool handled = false;
        ConsoleKeyInfo right = new('\0', ConsoleKey.RightArrow, false, false, false);
        ctrl.KeyPressed(right, ref handled);
        ctrl.KeyPressed(right, ref handled);
        ctrl.KeyPressed(right, ref handled);

        Control? focusedPaneBefore = GetFocusedPane(ctrl);

        handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);

        Assert.Same(focusedPaneBefore, GetFocusedPane(ctrl));

        handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false), ref handled);

        Assert.Same(focusedPaneBefore, GetFocusedPane(ctrl));

        ctrl.Unload();
    }

    private static Control? GetFocusedPane(SummaryControl2 ctrl)
    {
        MethodInfo? method = typeof(SummaryControl2).GetMethod(
            "FindFocusedPane", BindingFlags.NonPublic | BindingFlags.Instance);

        return (Control?)method!.Invoke(ctrl, null);
    }
}
