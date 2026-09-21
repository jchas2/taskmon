using System.Reflection;
using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ProcessesControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public ProcessesControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        ProcessesControl ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Constructor_With_Null_Terminal_Throws_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new ProcessesControl(
                runContext.ServiceController,
                null!,
                runContext.AppConfig));

    [Fact]
    public void Resize_Splits_Sixty_Forty_Top_To_Bottom_At_Full_Width()
    {
        const int width = 120;
        const int height = 40;

        ProcessesControl ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = width,
            Height = height
        };

        ctrl.Load();
        ctrl.Resize();

        Assert.Equal(width, ctrl.ProcessControl.Width);
        Assert.Equal(width, ctrl.ProcessInfoControl.Width);

        Assert.Equal(ctrl.Y, ctrl.ProcessControl.Y);
        Assert.Equal((int)(height * 0.6), ctrl.ProcessControl.Height);

        // Stacked directly below processControl, with no gap, never overlapping it.
        Assert.Equal(ctrl.ProcessControl.Y + ctrl.ProcessControl.Height, ctrl.ProcessInfoControl.Y);
        Assert.Equal(height - ctrl.ProcessControl.Height, ctrl.ProcessInfoControl.Height);

        ctrl.Unload();
    }

    // SetFocus() is a no-op without a parent Screen to route through (GetParentScreen() finds
    // nothing to call FocusInternal on), so the focus-routing tests need one.
    private ProcessesControl CreateFocusableControl(int width = 120, int height = 40)
    {
        ForwardingTerminal terminal = new(runContext.Terminal);
        Screen screen = new(terminal) { Width = width, Height = height };

        ProcessesControl ctrl = new(
            runContext.ServiceController,
            terminal,
            runContext.AppConfig) {
            Width = width,
            Height = height
        };

        screen.Controls.Add(ctrl);
        ctrl.Load();
        ctrl.Resize();
        ctrl.SetFocus();

        return ctrl;
    }

    // Regression test for a real bug: ProcessInfoControl's own menuView.ColumnHeaders[0].Width
    // was set to the outer control width instead of the inner content width (border columns
    // excluded), which made ListView.DrawItem's columnWidth-vs-viewport guard trip on every row
    // and silently blank all menu item text - the menu rendered its border but no labels.
    [Fact]
    public void Draw_Shows_ProcessInfoControls_Menu_Labels()
    {
        ProcessesControl ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = 160,
            Height = 40
        };

        ctrl.Load();
        ctrl.Resize();
        ctrl.Draw();

        string output = string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

        Assert.Contains("SELECT", output);
        Assert.Contains("DETAIL", output);
        Assert.Contains("THREADS", output);
        Assert.Contains("MODULES", output);
        Assert.Contains("HANDLES", output);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
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

    private static SystemSnapshot BuildSnapshot(params int[] pids) =>
        BuildSnapshot(pids.Select((pid, i) => (pid, cpu: 1.0 - i * 0.1)).ToArray());

    // Default sort column is Cpu, descending - so the pid with the highest cpu value sorts to
    // index 0, letting a test move "whoever is selected" without any key press, matching a
    // background sort re-publish rather than user input.
    private static SystemSnapshot BuildSnapshot(params (int pid, double cpu)[] entries) =>
        new() {
            Processes = new ProcessInfo {
                Metrics = new ProcessMetrics {
                    Entries = entries.Select(e => new ProcessEntry {
                        Pid = e.pid,
                        ProcessName = $"proc{e.pid}",
                        FileDescription = $"Process {e.pid}",
                        CpuTimePercent = e.cpu,
                    }).ToList(),
                },
            },
        };

    [Fact]
    public void Initial_Selection_Syncs_To_ProcessInfoControl()
    {
        ProcessesControl ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = 160,
            Height = 40
        };

        ctrl.Load();
        ctrl.Resize();

        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));

        Assert.Equal(1111, ctrl.ProcessControl.SelectedProcessId);
        Assert.Equal(1111, ctrl.ProcessInfoControl.SelectedProcessId);

        ctrl.Unload();
    }

    // Regression test for a real bug: arrow-key movement inside processView never reached
    // ProcessControl.OnDraw() (ListView repaints just the two affected rows directly instead of
    // going through a full Draw()), so the only existing hook - a diff check at the end of
    // OnDraw() - only ever caught snapshot-driven selection changes, never arrow-driven ones.
    [Fact]
    public void Arrow_Down_Syncs_To_ProcessInfoControl()
    {
        ProcessesControl ctrl = new(
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = 160,
            Height = 40
        };

        ctrl.Load();
        ctrl.Resize();

        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));

        bool handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);

        Assert.Equal(2222, ctrl.ProcessControl.SelectedProcessId);
        Assert.Equal(2222, ctrl.ProcessInfoControl.SelectedProcessId);

        ctrl.Unload();
    }

    [Fact]
    public void RightArrow_On_ProcessControl_Moves_Focus_Into_ProcessInfoControl()
    {
        ProcessesControl ctrl = CreateFocusableControl();

        Assert.True(ctrl.ProcessControl.HasFocus);

        bool handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);

        Assert.True(handled);
        Assert.True(ctrl.ProcessInfoControl.HasFocus);

        ctrl.Unload();
    }

    [Fact]
    public void LeftArrow_Walks_Back_Out_Through_ProcessInfoControls_Own_Menu_To_ProcessControl()
    {
        ProcessesControl ctrl = CreateFocusableControl();

        bool handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);
        Assert.True(handled);

        // A second RightArrow moves focus from ProcessInfoControl's own menu into whichever tab
        // is active (DETAIL, by default).
        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);
        Assert.True(handled);

        // First LeftArrow: ProcessInfoControl claims it internally, stepping from its active tab
        // back to its own menu - focus should still be somewhere inside ProcessInfoControl.
        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.LeftArrow), ref handled);
        Assert.True(handled);
        Assert.True(ctrl.ProcessInfoControl.HasFocus);

        // Second LeftArrow: ProcessInfoControl's menu has nowhere further left to go, so this
        // bubbles out to ProcessesControl, which steps focus back to processControl.
        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.LeftArrow), ref handled);
        Assert.True(handled);
        Assert.True(ctrl.ProcessControl.HasFocus);

        ctrl.Unload();
    }

    // Usability fix: while the user is scrolling one of ProcessInfoControl's own lists, a
    // background snapshot re-publish that reorders the pid grid (nothing the user did) must not
    // yank them onto a different pid mid-scroll.
    [Fact]
    public void Selection_Change_Is_Ignored_While_ProcessInfoControl_Has_Focus()
    {
        ProcessesControl ctrl = CreateFocusableControl();

        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot((1111, 0.9), (2222, 0.1)));
        Assert.Equal(1111, ctrl.ProcessInfoControl.SelectedProcessId);

        bool handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);
        Assert.True(ctrl.ProcessInfoControl.HasFocus);

        // 2222 now outranks 1111 on cpu - a resort that would move the pid grid's SelectedIndex=0
        // onto a different process, with no key press involved.
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot((2222, 0.9), (1111, 0.1)));

        Assert.Equal(2222, ctrl.ProcessControl.SelectedProcessId);
        Assert.Equal(1111, ctrl.ProcessInfoControl.SelectedProcessId);

        ctrl.Unload();
    }

    [Fact]
    public void Selection_Change_Catches_Up_When_Focus_Leaves_ProcessInfoControl()
    {
        ProcessesControl ctrl = CreateFocusableControl();

        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot((1111, 0.9), (2222, 0.1)));

        bool handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);

        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot((2222, 0.9), (1111, 0.1)));
        Assert.Equal(1111, ctrl.ProcessInfoControl.SelectedProcessId);

        // Walk back out: first LeftArrow only steps ProcessInfoControl's own menu <-> tab focus,
        // second LeftArrow is the one that actually leaves it.
        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.LeftArrow), ref handled);
        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.LeftArrow), ref handled);

        Assert.True(ctrl.ProcessControl.HasFocus);
        Assert.Equal(2222, ctrl.ProcessInfoControl.SelectedProcessId);

        ctrl.Unload();
    }

    // Pid auto-binding resumes once focus leaves ProcessInfoControl, so it must be DETAIL that's
    // on screen then - not MODULES, which can shell out (macOS) on every rebind.
    [Fact]
    public void Leaving_ProcessInfoControl_Resets_Menu_To_Detail()
    {
        ProcessesControl ctrl = CreateFocusableControl();

        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));

        bool handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);
        ctrl.ProcessInfoControl.SelectMenuItemForTests(2);

        Assert.False(ctrl.ProcessInfoControl.IsDetailActiveForTests);

        // Stepping into the MODULES tab and back to the menu stays inside ProcessInfoControl, so
        // the user's choice of tab must survive it.
        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.RightArrow), ref handled);
        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.LeftArrow), ref handled);

        Assert.True(ctrl.ProcessInfoControl.HasFocus);
        Assert.False(ctrl.ProcessInfoControl.IsDetailActiveForTests);

        // This LeftArrow actually leaves ProcessInfoControl.
        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.LeftArrow), ref handled);

        Assert.True(ctrl.ProcessControl.HasFocus);
        Assert.True(ctrl.ProcessInfoControl.IsDetailActiveForTests);
        Assert.Equal(0, ctrl.ProcessInfoControl.SelectedMenuIndexForTests);

        ctrl.Unload();
    }

    private static ConsoleKeyInfo SortKey => new('s', ConsoleKey.S, false, false, false);

    [Fact]
    public void S_Key_Toggles_Sort_Menu()
    {
        ProcessesControl ctrl = CreateFocusableControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));
        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        ctrl.KeyPressed(SortKey, ref handled);

        Assert.True(handled);
        Assert.True(ctrl.ProcessControl.IsSortSelectionActive);
        Assert.True(ctrl.ProcessControl.HasFocus);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("SORT BY"))), Times.AtLeastOnce);

        handled = false;
        ctrl.KeyPressed(SortKey, ref handled);

        Assert.True(handled);
        Assert.False(ctrl.ProcessControl.IsSortSelectionActive);

        ctrl.Unload();
    }

    [Fact]
    public void Enter_On_Sort_Menu_Changes_Sort_And_Closes_Menu()
    {
        ProcessesControl ctrl = CreateFocusableControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));

        bool handled = false;
        ctrl.KeyPressed(SortKey, ref handled);

        // Sort items are the visible columns in Columns order - PROCESS, then PID - so one step
        // down from the top lands on PID, which no default config sorts by.
        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);
        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.Equal(Statistics.Pid, runContext.AppConfig.SortColumn);
        Assert.False(ctrl.ProcessControl.IsSortSelectionActive);
        Assert.True(ctrl.ProcessControl.HasFocus);

        ctrl.Unload();
    }

    // The sort menu is modal - left/right must neither leave it open behind focus moving into the
    // info pane (RightArrow) nor bubble out towards the main menu (LeftArrow).
    [Theory]
    [InlineData(ConsoleKey.RightArrow)]
    [InlineData(ConsoleKey.LeftArrow)]
    public void Left_Right_Arrows_Do_Not_Leave_Open_Sort_Menu(ConsoleKey key)
    {
        ProcessesControl ctrl = CreateFocusableControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));

        bool handled = false;
        ctrl.KeyPressed(SortKey, ref handled);

        handled = false;
        ctrl.KeyPressed(ControlHelper.GetConsoleKeyInfo(key), ref handled);

        Assert.True(handled);
        Assert.True(ctrl.ProcessControl.IsSortSelectionActive);
        Assert.True(ctrl.ProcessControl.HasFocus);
        Assert.False(ctrl.ProcessInfoControl.HasFocus);

        ctrl.Unload();
    }

    [Fact]
    public void Footer_Shows_Sort_And_Scroll_Hints()
    {
        ProcessesControl ctrl = CreateFocusableControl();
        RaiseSnapshotUpdated(runContext.ServiceController, BuildSnapshot(1111, 2222));
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Draw();

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains(ProcessControl.DefaultFooterText))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Sort Asc: a"))), Times.Never);

        ctrl.Unload();
    }
}
