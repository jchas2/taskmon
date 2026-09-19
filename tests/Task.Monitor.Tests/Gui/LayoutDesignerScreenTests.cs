using System.Drawing;
using System.Reflection;
using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui;

public sealed class LayoutDesignerScreenTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public LayoutDesignerScreenTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private static ConsoleKeyInfo Key(ConsoleKey key, bool ctrl = false) =>
        new('\0', key, shift: false, alt: false, control: ctrl);

    private LayoutDesignerScreen CreateScreen(int width = 120, int height = 40)
    {
        LayoutDesignerScreen screen = new(runContext) {
            Width = width,
            Height = height
        };

        screen.Load();
        screen.Resize();
        return screen;
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        LayoutDesignerScreen screen = new(runContext);

        Assert.NotNull(screen);
        Assert.Null(screen.LayoutName);
    }

    [Fact]
    public void Draws_The_Example_Trees_Panes_And_Banner()
    {
        LayoutDesignerScreen screen = CreateScreen();
        screen.Draw();

        string output = CapturedOutput();

        Assert.Contains("LAYOUT DESIGNER", output);
        Assert.Contains("Untitled", output);
        Assert.Contains("Cpu", output);
        Assert.Contains("Memory", output);
        Assert.Contains("Gpu", output);

        screen.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    private static bool AnyDescendantFocused(Control control) =>
        control.Focused || control.Controls.Any(AnyDescendantFocused);

    // Regression test: ProcessControl and SystemInfoControl used to grab their own internal
    // focus unconditionally in OnLoad (processView.SetFocus() / navMenu.SetFocus()) - fine for
    // their usual standalone hosting, where a subsequent OnGotFocus-driven SetFocus() from the
    // host masks it, but fatal here, where nothing ever calls SetFocus() on a pane at all
    // (selectedNodeId is a designer-owned cursor, not real focus - see the class comment). That
    // stray real focus produced a second, permanent "orange border" on top of the designer's own
    // selection overlay, on whichever pane happened to grab it during Load.
    [Fact]
    public void No_Pane_Holds_Real_Focus_After_Load()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.FromNodes([
            new SummaryLayoutNode {
                Id = 0, IsSplit = true, Orientation = Orientation.Row, Ratio = 0.5f, FirstId = 1, SecondId = 2
            },
            new SummaryLayoutNode { Id = 1, ControlType = PaneControlType.Cpu },
            new SummaryLayoutNode { Id = 2, ControlType = PaneControlType.Process },
        ], rootId: 0);

        LayoutDesignerScreen screen = new(runContext) { Width = 120, Height = 40 };
        screen.Open(tree, null);
        screen.Load();
        screen.Resize();

        foreach (Control control in screen.Controls) {
            Assert.False(AnyDescendantFocused(control));
        }

        screen.Unload();
    }

    // Reflection is the only way to raise this from a test: ServiceController only ever raises it
    // itself, from inside its real worker loop.
    private void RaiseSnapshotUpdated(SystemSnapshot snapshot)
    {
        MulticastDelegate? handler = (MulticastDelegate?)typeof(ServiceController)
            .GetField("SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(runContext.ServiceController);

        handler?.DynamicInvoke(runContext.ServiceController, new SystemSnapshotEventArgs(snapshot));
    }

    private static IEnumerable<Control> Subtree(Control control) =>
        new[] { control }.Concat(control.Controls.SelectMany(Subtree));

    // Cpu on the left, Process on the right - Right arrow from the default selection (Cpu, first
    // in tree order) lands on the Process pane.
    private LayoutDesignerScreen CreateCpuProcessScreen()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.FromNodes([
            new SummaryLayoutNode {
                Id = 0, IsSplit = true, Orientation = Orientation.Row, Ratio = 0.5f, FirstId = 1, SecondId = 2
            },
            new SummaryLayoutNode { Id = 1, ControlType = PaneControlType.Cpu },
            new SummaryLayoutNode { Id = 2, ControlType = PaneControlType.Process },
        ], rootId: 0);

        LayoutDesignerScreen screen = new(runContext) { Width = 120, Height = 40 };
        screen.Open(tree, null);
        screen.Load();
        screen.Resize();
        return screen;
    }

    // Regression test: selection used to be an outline painted over the pane from outside, which
    // flickered - a pane with a live snapshot subscription (Process, or a chart fed on the tick)
    // repaints its own default-coloured border on every tick, and the outline could only go back
    // on after that. The selected pane now draws its own borders in the focus colour, so there is
    // nothing to repaint afterwards. Walks the whole subtree because ProcessControl draws no
    // border itself - its inner, active ListView does.
    [Fact]
    public void The_Selected_Pane_Draws_Every_Border_In_Its_Subtree_In_The_Focus_Colour()
    {
        LayoutDesignerScreen screen = CreateCpuProcessScreen();

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);
        Assert.Equal(2, screen.SelectedNodeId);

        Control processPane = screen.Controls.Single(c => c.GetType().Name == "ProcessControl");
        Control cpuPane = screen.Controls.Single(c => c.GetType().Name == "Chart");

        Assert.All(Subtree(processPane), c => Assert.Equal(Control.FocusSelectionColour, c.BorderColour));
        Assert.Equal(runContext.AppConfig.Theme.ChartBorder, cpuPane.BorderColour);

        screen.Unload();
    }

    [Fact]
    public void Moving_The_Selection_Away_Restores_The_Previous_Panes_Own_Border_Colours()
    {
        LayoutDesignerScreen screen = CreateCpuProcessScreen();

        Control processPane = screen.Controls.Single(c => c.GetType().Name == "ProcessControl");
        List<(Control Control, Color Colour)> unselected =
            Subtree(processPane).Select(c => (c, c.BorderColour)).ToList();

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled); // onto Process
        screen.KeyPressed(Key(ConsoleKey.LeftArrow), ref handled);  // back off it

        Assert.All(unselected, pair => Assert.Equal(pair.Colour, pair.Control.BorderColour));

        screen.Unload();
    }

    // The flicker the user saw happened on exactly this path: a tick redraws the Process pane by
    // itself. The highlight is a property the pane's own redraw reads, so it survives the tick.
    [Fact]
    public void The_Highlight_Survives_A_Snapshot_Tick()
    {
        LayoutDesignerScreen screen = CreateCpuProcessScreen();

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);

        RaiseSnapshotUpdated(new SystemSnapshot {
            Processes = new Task.Monitor.System.Services.Process.ProcessInfo {
                Metrics = new Task.Monitor.System.Services.Process.ProcessMetrics {
                    Entries = [
                        new Task.Monitor.System.Services.Process.ProcessEntry {
                            Pid = 4242, ProcessName = "proc4242", FileDescription = "Test Process", CpuTimePercent = 0.5
                        },
                    ],
                },
            },
        });

        Control processPane = screen.Controls.Single(c => c.GetType().Name == "ProcessControl");
        Assert.All(Subtree(processPane), c => Assert.Equal(Control.FocusSelectionColour, c.BorderColour));

        screen.Unload();
    }

    private int SnapshotSubscriberCount(string controlTypeName)
    {
        MulticastDelegate? handler = (MulticastDelegate?)typeof(ServiceController)
            .GetField("SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(runContext.ServiceController);

        return handler?.GetInvocationList().Count(d => d.Target?.GetType().Name == controlTypeName) ?? 0;
    }

    // Regression test for the reported "replacing the bottom Process pane with Drivers flickers
    // and jumps around": OnLoad Load()ed every pane explicitly AND via base.OnLoad() (Control's
    // default OnLoad loads every child), so the Process pane subscribed to snapshot updates twice.
    // Replacing it Unload()ed once, removing only one subscription - the detached ProcessControl
    // kept redrawing itself at its old position, underneath the new Drivers pane.
    [Fact]
    public void Replacing_A_Loaded_Process_Pane_Leaves_No_Process_Subscription_Behind()
    {
        LayoutDesignerScreen screen = CreateCpuProcessScreen();

        Assert.Equal(1, SnapshotSubscriberCount("ProcessControl"));

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled); // onto Process
        screen.KeyPressed(Key(ConsoleKey.Enter), ref handled);      // picker opens on Process
        screen.KeyPressed(Key(ConsoleKey.DownArrow), ref handled);  // Process -> Drivers
        screen.KeyPressed(Key(ConsoleKey.Enter), ref handled);

        Assert.Equal(PaneControlType.Drivers, screen.Tree.Nodes[screen.SelectedNodeId].ControlType);
        Assert.Equal(0, SnapshotSubscriberCount("ProcessControl"));
        Assert.Equal(1, SnapshotSubscriberCount("DriversControl"));

        screen.Unload();

        Assert.Equal(0, SnapshotSubscriberCount("DriversControl"));
    }

    [Fact]
    public void A_Freshly_Split_Empty_Pane_Is_Selected_And_Highlighted()
    {
        LayoutDesignerScreen screen = CreateScreen();

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.RightArrow, ctrl: true), ref handled);

        Assert.Equal(PaneControlType.Empty, screen.Tree.Nodes[screen.SelectedNodeId].ControlType);

        Control emptyPane = screen.Controls.Single(c => c is EmptyPaneControl);
        Assert.Equal(Control.FocusSelectionColour, emptyPane.BorderColour);
        Assert.Contains("empty", CapturedOutput());

        screen.Unload();
    }

    // Regression test: charts are passive (they only move when something calls Add() on them),
    // unlike Process/Drivers/Services panes which subscribe to snapshots themselves - the designer
    // never fed its chart panes, so they previewed as permanently empty.
    [Fact]
    public void A_Published_Snapshot_Feeds_The_Chart_Panes()
    {
        LayoutDesignerScreen screen = CreateScreen();
        runContext.AppConfig.ShowMetreCpuNumerically = true;
        runContextHelper.terminal.Invocations.Clear();

        RaiseSnapshotUpdated(new SystemSnapshot {
            Cpu = new Task.Monitor.System.Services.Cpu.CpuInfo {
                Metrics = new Task.Monitor.System.Services.Cpu.CpuMetrics {
                    CpuPercentKernelTime = 0.1,
                    CpuPercentUserTime = 0.2,
                }
            }
        });

        Assert.Contains("Kernel", CapturedOutput());

        screen.Unload();
    }

    [Fact]
    public void Open_Replaces_The_Tree_And_Resets_The_Layout_Name()
    {
        LayoutDesignerScreen screen = CreateScreen();

        SummaryLayoutTree freshTree = SummaryLayoutTree.CreateExample();
        screen.Open(freshTree, "My Dashboard");

        Assert.Same(freshTree, screen.Tree);
        Assert.Equal("My Dashboard", screen.LayoutName);
        Assert.Equal(freshTree.Panes().First().Id, screen.SelectedNodeId);

        screen.Unload();
    }

    [Fact]
    public void Right_Arrow_Moves_Selection_To_The_Pane_On_The_Right()
    {
        LayoutDesignerScreen screen = CreateScreen();
        int startId = screen.SelectedNodeId;

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);

        Assert.True(handled);
        Assert.NotEqual(startId, screen.SelectedNodeId);

        screen.Unload();
    }

    [Fact]
    public void Ctrl_Right_Splits_The_Selected_Pane_And_Selects_The_New_Empty_Pane()
    {
        LayoutDesignerScreen screen = CreateScreen();
        int originalPaneId = screen.SelectedNodeId;
        int nodeCountBefore = screen.Tree.Nodes.Count;

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.RightArrow, ctrl: true), ref handled);

        Assert.True(handled);
        Assert.Equal(nodeCountBefore + 2, screen.Tree.Nodes.Count);

        SummaryLayoutNode splitNode = screen.Tree.Nodes[originalPaneId];
        Assert.True(splitNode.IsSplit);
        Assert.Equal(Orientation.Row, splitNode.Orientation);

        SummaryLayoutNode selectedPane = screen.Tree.Nodes[screen.SelectedNodeId];
        Assert.False(selectedPane.IsSplit);
        Assert.Equal(PaneControlType.Empty, selectedPane.ControlType);

        screen.Unload();
    }

    [Fact]
    public void Ctrl_Down_Splits_Column_Wise()
    {
        LayoutDesignerScreen screen = CreateScreen();
        int originalPaneId = screen.SelectedNodeId;

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.DownArrow, ctrl: true), ref handled);

        Assert.True(handled);
        Assert.Equal(Orientation.Column, screen.Tree.Nodes[originalPaneId].Orientation);

        screen.Unload();
    }

    [Fact]
    public void Enter_Then_Picking_Cpu_Reassigns_The_Selected_Empty_Pane()
    {
        LayoutDesignerScreen screen = CreateScreen();

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.RightArrow, ctrl: true), ref handled); // split -> new Empty pane selected
        screen.KeyPressed(Key(ConsoleKey.Enter), ref handled); // open the control-type picker

        // PaneControlType order is Empty, Cpu, Memory, ... - one Down from Empty reaches Cpu.
        screen.KeyPressed(Key(ConsoleKey.DownArrow), ref handled);
        screen.KeyPressed(Key(ConsoleKey.Enter), ref handled); // confirm

        Assert.Equal(PaneControlType.Cpu, screen.Tree.Nodes[screen.SelectedNodeId].ControlType);

        runContextHelper.terminal.Invocations.Clear();
        screen.Draw();

        Assert.Contains("Cpu", CapturedOutput());

        screen.Unload();
    }

    [Fact]
    public void Escape_Cancels_The_Control_Type_Picker_Without_Reassigning()
    {
        LayoutDesignerScreen screen = CreateScreen();

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.RightArrow, ctrl: true), ref handled);
        int emptyPaneId = screen.SelectedNodeId;

        screen.KeyPressed(Key(ConsoleKey.Enter), ref handled);
        screen.KeyPressed(Key(ConsoleKey.DownArrow), ref handled);
        screen.KeyPressed(Key(ConsoleKey.Escape), ref handled);

        Assert.Equal(PaneControlType.Empty, screen.Tree.Nodes[emptyPaneId].ControlType);

        screen.Unload();
    }

    [Fact]
    public void C_Does_Nothing_When_The_Selected_Pane_Is_Not_A_Process_Pane()
    {
        LayoutDesignerScreen screen = CreateScreen();

        // The default selection (first pane in tree order) is the Cpu chart, not the Process pane.
        Assert.NotEqual(PaneControlType.Process, screen.Tree.Nodes[screen.SelectedNodeId].ControlType);

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.C), ref handled);

        Assert.True(handled);

        // Nothing crashes and no picker opens - a second key press just does regular navigation.
        screen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);
        Assert.True(handled);

        screen.Unload();
    }

    [Fact]
    public void C_On_A_Process_Pane_Opens_The_Column_Picker_And_Enter_Applies_The_Checked_Columns()
    {
        LayoutDesignerScreen screen = CreateScreen();

        // A minimal two-pane tree with Process as the first pane in tree order, so it's already
        // selected on Open() - avoids needing to reason about the example tree's spatial layout
        // (arrow movement here is spatial, not the tree-order cycling SummaryControl2 uses).
        const int processPaneId = 1;

        SummaryLayoutTree tree = SummaryLayoutTree.FromNodes([
            new SummaryLayoutNode {
                Id = 0, IsSplit = true, Orientation = Orientation.Row, Ratio = 0.5f, FirstId = processPaneId, SecondId = 2
            },
            new SummaryLayoutNode {
                Id = processPaneId, ControlType = PaneControlType.Process,
                ProcessColumns = Statistics.Process | Statistics.Pid
            },
            new SummaryLayoutNode { Id = 2, ControlType = PaneControlType.Cpu },
        ], rootId: 0);

        screen.Open(tree, null);

        Assert.Equal(processPaneId, screen.SelectedNodeId);

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.C), ref handled);
        // Toggle the first row ("User") on with Space, then confirm.
        screen.KeyPressed(Key(ConsoleKey.Spacebar), ref handled);
        screen.KeyPressed(Key(ConsoleKey.Enter), ref handled);

        Statistics? columns = screen.Tree.Nodes[processPaneId].ProcessColumns;

        Assert.NotNull(columns);
        Assert.True((columns!.Value & Statistics.User) != 0);
        Assert.True((columns.Value & Statistics.Process) != 0);
        Assert.True((columns.Value & Statistics.Pid) != 0);

        screen.Unload();
    }

    [Fact]
    public void Delete_On_The_Root_Pane_Does_Nothing()
    {
        // A tree with a single pane (the root itself) - nothing to merge it into.
        SummaryLayoutTree singlePaneTree = SummaryLayoutTree.FromNodes(
            [new SummaryLayoutNode { Id = 0, ControlType = PaneControlType.Cpu }], rootId: 0);

        LayoutDesignerScreen screen = CreateScreen();
        screen.Open(singlePaneTree, null);

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.Delete), ref handled);

        Assert.True(handled);
        Assert.True(screen.Tree.Nodes.ContainsKey(0));

        screen.Unload();
    }

    [Fact]
    public void Delete_Removes_The_Selected_Pane_And_Selection_Moves_To_The_Promoted_Sibling()
    {
        LayoutDesignerScreen screen = CreateScreen();
        int startingPaneCount = screen.Tree.Panes().Count();
        int selectedBefore = screen.SelectedNodeId;

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.Delete), ref handled);

        Assert.True(handled);
        Assert.Equal(startingPaneCount - 1, screen.Tree.Panes().Count());
        Assert.NotEqual(selectedBefore, screen.SelectedNodeId);
        Assert.False(screen.Tree.Nodes[screen.SelectedNodeId].IsSplit);

        screen.Unload();
    }

    [Fact]
    public void Plus_And_Minus_Adjust_The_Parent_Splits_Ratio()
    {
        LayoutDesignerScreen screen = CreateScreen();
        int? parentId = screen.Tree.FindParentSplitId(screen.SelectedNodeId);
        Assert.NotNull(parentId);

        float ratioBefore = screen.Tree.Nodes[parentId!.Value].Ratio;

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.Add), ref handled);

        Assert.True(handled);
        Assert.NotEqual(ratioBefore, screen.Tree.Nodes[parentId.Value].Ratio);

        screen.Unload();
    }

    // Typed with each letter's real ConsoleKey (not a placeholder key) - so the 'S' in "My Stats"
    // arrives as ConsoleKey.S, the save key itself, proving the save-name prompt swallows it
    // rather than it re-triggering a save mid-typing.
    private static ConsoleKeyInfo Typed(char ch) =>
        new(ch, char.IsLetter(ch) ? Enum.Parse<ConsoleKey>(char.ToUpperInvariant(ch).ToString()) : ConsoleKey.Spacebar,
            shift: char.IsUpper(ch), alt: false, control: false);

    // Plain S rather than Ctrl+S: consoles take Ctrl+S as "pause output" (XOFF) and it never
    // reaches the app - the reported "screen hangs until the next key, no save dialog".
    [Fact]
    public void S_With_No_Name_Prompts_And_Then_Saves_On_Enter()
    {
        runContextHelper.fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        LayoutDesignerScreen screen = CreateScreen();

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.S), ref handled);
        Assert.True(handled);
        Assert.Null(screen.LayoutName); // still unnamed - only the prompt has opened so far
        Assert.Contains("Save Layout As", CapturedOutput());

        foreach (char ch in "My Stats") {
            screen.KeyPressed(Typed(ch), ref handled);
        }

        screen.KeyPressed(Key(ConsoleKey.Enter), ref handled);

        Assert.Equal("My Stats", screen.LayoutName);
        Assert.Contains(runContext.AppConfig.SummaryLayouts2, l => l.Name == "My Stats");

        screen.Unload();
    }

    [Fact]
    public void Escape_In_The_Save_Dialog_Cancels_Without_Saving()
    {
        runContextHelper.fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        LayoutDesignerScreen screen = CreateScreen();

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.S), ref handled);

        foreach (char ch in "Nope") {
            screen.KeyPressed(Typed(ch), ref handled);
        }

        screen.KeyPressed(Key(ConsoleKey.Escape), ref handled);

        Assert.True(handled); // swallowed by the dialog - not left to bubble out and close the screen
        Assert.Null(screen.LayoutName);
        Assert.DoesNotContain(runContext.AppConfig.SummaryLayouts2, l => l.Name == "Nope");

        // Back in normal designer mode: arrows move the selection again.
        int before = screen.SelectedNodeId;
        screen.KeyPressed(Key(ConsoleKey.RightArrow), ref handled);
        Assert.NotEqual(before, screen.SelectedNodeId);

        screen.Unload();
    }

    [Fact]
    public void Tab_To_Cancel_In_The_Save_Dialog_Then_Enter_Does_Not_Save()
    {
        runContextHelper.fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        LayoutDesignerScreen screen = CreateScreen();

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.S), ref handled);

        foreach (char ch in "Nope") {
            screen.KeyPressed(Typed(ch), ref handled);
        }

        screen.KeyPressed(Key(ConsoleKey.Tab), ref handled);
        screen.KeyPressed(Key(ConsoleKey.Enter), ref handled);

        Assert.Null(screen.LayoutName);
        Assert.DoesNotContain(runContext.AppConfig.SummaryLayouts2, l => l.Name == "Nope");

        screen.Unload();
    }

    // A layout's name becomes its [section] name in the saved .layout file, and ConfigParser only
    // accepts letters, digits, '-' and spaces there - e.g. "My_Dash" would save, then fail to parse
    // on the next load. So the save dialog won't accept anything else.
    [Fact]
    public void The_Save_Dialog_Rejects_Characters_A_Layout_Section_Name_Cannot_Hold()
    {
        runContextHelper.fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        LayoutDesignerScreen screen = CreateScreen();

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.S), ref handled);

        foreach (char ch in "My_Dash.1") {
            screen.KeyPressed(new ConsoleKeyInfo(ch, char.IsLetter(ch)
                ? Enum.Parse<ConsoleKey>(char.ToUpperInvariant(ch).ToString())
                : ConsoleKey.Oem1, false, false, false), ref handled);
        }

        screen.KeyPressed(Key(ConsoleKey.Enter), ref handled);

        Assert.Equal("MyDash1", screen.LayoutName);

        screen.Unload();
    }

    // Regression test: re-saving an already-named layout used to write straight over it with no
    // dialog - and no visible change either (the banner already showed the name), so S looked
    // dead. It now always prompts, pre-filled with the current name.
    [Fact]
    public void S_With_An_Existing_Name_Opens_The_Dialog_Prefilled_And_Enter_Overwrites()
    {
        runContextHelper.fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        LayoutDesignerScreen screen = CreateScreen();
        screen.Open(SummaryLayoutTree.CreateExample(), "Already Named");
        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.S), ref handled);

        Assert.True(handled);
        Assert.Contains("Save Layout As", CapturedOutput());
        Assert.Contains("Already Named", CapturedOutput()); // the pre-filled field
        Assert.DoesNotContain(runContext.AppConfig.SummaryLayouts2, l => l.Name == "Already Named"); // nothing saved yet

        screen.KeyPressed(Key(ConsoleKey.Enter), ref handled);

        Assert.Equal("Already Named", screen.LayoutName);
        Assert.Contains(runContext.AppConfig.SummaryLayouts2, l => l.Name == "Already Named");

        screen.Unload();
    }

    [Fact]
    public void Editing_The_Prefilled_Name_Saves_A_Copy_Under_The_New_Name()
    {
        runContextHelper.fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        LayoutDesignerScreen screen = CreateScreen();
        screen.Open(SummaryLayoutTree.CreateExample(), "Original");

        bool handled = false;
        screen.KeyPressed(Key(ConsoleKey.S), ref handled);

        foreach (char ch in " Copy") {
            screen.KeyPressed(Typed(ch), ref handled);
        }

        screen.KeyPressed(Key(ConsoleKey.Enter), ref handled);

        Assert.Equal("Original Copy", screen.LayoutName);
        Assert.Contains(runContext.AppConfig.SummaryLayouts2, l => l.Name == "Original Copy");

        screen.Unload();
    }
}
