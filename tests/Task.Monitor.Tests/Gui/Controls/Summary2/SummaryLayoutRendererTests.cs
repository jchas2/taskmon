using System.Drawing;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

// Confirms the renderer's recursive split math against SummaryLayoutTree.CreateExample()'s shape:
// a row of three panes (Cpu | Memory | Gpu) over one Process pane, nested as
// Column(Row(Cpu, Row(Memory, Gpu)), Process).
public sealed class SummaryLayoutRendererTests
{
    private const int MetreHeight = 3;

    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    private sealed class FixedPane(ISystemTerminal terminal, int height) : Control(terminal), IFixedHeightPane
    {
        public int FixedHeight => height;
    }

    public SummaryLayoutRendererTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private static void AssertBounds(
        SummaryLayoutRenderer renderer,
        SummaryLayoutTree tree,
        PaneControlType controlType,
        int x, int y, int width, int height)
    {
        SummaryLayoutNode pane = tree.Panes().Single(p => p.ControlType == controlType);
        Rectangle bounds = renderer.PaneBounds[pane.Id];

        Assert.Equal(new Rectangle(x, y, width, height), bounds);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    public void A_Chain_Of_Metres_Stacks_Without_Gaps_Above_The_Process_Pane(int height)
    {
        // CpuCores over GpuMemory over Swap over Disk over Process, as in a hand-built htop-style layout.
        SummaryLayoutTree tree = SummaryLayoutTree.FromNodes([
            Split(0, Orientation.Column, 0.2f,  1, 2), Pane(1),
            Split(2, Orientation.Column, 0.1f,  3, 4), Pane(3),
            Split(4, Orientation.Column, 0.1f,  5, 6), Pane(5),
            Split(6, Orientation.Column, 0.25f, 7, 8), Pane(7), Pane(8),
        ], rootId: 0);

        Dictionary<int, Rectangle> bounds = LayoutWithFixed(tree, [3, 5, 7], height);

        int coresHeight = (int)(height * 0.2f);

        Assert.Equal(coresHeight, bounds[1].Height);
        Assert.Equal(new Rectangle(0, coresHeight,                   120, MetreHeight), bounds[3]);
        Assert.Equal(new Rectangle(0, coresHeight + MetreHeight,     120, MetreHeight), bounds[5]);
        Assert.Equal(new Rectangle(0, coresHeight + MetreHeight * 2, 120, MetreHeight), bounds[7]);
        Assert.Equal(new Rectangle(0, coresHeight + MetreHeight * 3, 120, height - coresHeight - MetreHeight * 3), bounds[8]);
    }

    [Fact]
    public void A_Fixed_Pane_Below_A_Ratio_Pane_Takes_Exactly_Its_Rows_At_The_Bottom()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.FromNodes([Split(0, Orientation.Column, 0.5f, 1, 2), Pane(1), Pane(2)], rootId: 0);

        Dictionary<int, Rectangle> bounds = LayoutWithFixed(tree, [2], height: 40);

        Assert.Equal(new Rectangle(0, 0,  120, 37), bounds[1]);
        Assert.Equal(new Rectangle(0, 37, 120, 3),  bounds[2]);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    [InlineData(60)]
    public void A_Fixed_Pane_On_Top_Takes_Exactly_Its_Rows_Whatever_The_Ratio(int height)
    {
        SummaryLayoutTree tree = SummaryLayoutTree.FromNodes([Split(0, Orientation.Column, 0.1f, 1, 2), Pane(1), Pane(2)], rootId: 0);

        Dictionary<int, Rectangle> bounds = LayoutWithFixed(tree, [1], height);

        Assert.Equal(new Rectangle(0, 0,           120, MetreHeight),          bounds[1]);
        Assert.Equal(new Rectangle(0, MetreHeight, 120, height - MetreHeight), bounds[2]);
    }

    [Fact]
    public void Clamps_Fixed_Panes_When_There_Is_Not_Enough_Room()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.FromNodes([
            Split(0, Orientation.Column, 0.5f, 1, 2), Split(1, Orientation.Column, 0.5f, 3, 4), Pane(2), Pane(3), Pane(4),
        ], rootId: 0);

        Dictionary<int, Rectangle> bounds = LayoutWithFixed(tree, [3, 4], height: 4);

        Assert.All(bounds.Values, rect => Assert.True(rect.Height >= 0));
        Assert.Equal(4, bounds[2].Height + bounds[3].Height + bounds[4].Height);
        Assert.Equal(MetreHeight, bounds[3].Height);
    }

    [Fact]
    public void Computes_Expected_Rects_For_The_Example_Tree()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        Dictionary<int, Control> paneControls = tree.Panes()
            .ToDictionary(pane => pane.Id, _ => new Control(runContext.Terminal));

        SummaryLayoutRenderer renderer = new();
        renderer.Layout(tree, paneControls, x: 0, y: 0, width: 120, height: 40);

        // Root Column split, ratio 0.6: top row gets 24 rows, Process gets the remaining 16.
        // Top row split (ratio 1/3): Cpu gets 40 cols, the Memory|Gpu row gets the remaining 80.
        // Memory|Gpu split 50/50 within that 80: 40 cols each.
        AssertBounds(renderer, tree, PaneControlType.Cpu, 0, 0, 40, 24);
        AssertBounds(renderer, tree, PaneControlType.Memory, 40, 0, 40, 24);
        AssertBounds(renderer, tree, PaneControlType.Gpu, 80, 0, 40, 24);
        AssertBounds(renderer, tree, PaneControlType.Process, 0, 24, 120, 16);
    }

    private Dictionary<int, Rectangle> LayoutWithFixed(SummaryLayoutTree tree, int[] fixedIds, int height)
    {
        Dictionary<int, Control> paneControls = tree.Panes().ToDictionary(
            pane => pane.Id,
            pane => fixedIds.Contains(pane.Id) ? new FixedPane(runContext.Terminal, MetreHeight) : new Control(runContext.Terminal));

        SummaryLayoutRenderer renderer = new();
        renderer.Layout(tree, paneControls, x: 0, y: 0, width: 120, height: height);

        return renderer.PaneBounds.ToDictionary();
    }

    private static SummaryLayoutNode Pane(int id) => new() { Id = id, ControlType = PaneControlType.Empty };

    [Fact]
    public void Sets_The_Pane_Controls_Own_Bounds_To_Match()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        Dictionary<int, Control> paneControls = tree.Panes()
            .ToDictionary(pane => pane.Id, _ => new Control(runContext.Terminal));

        SummaryLayoutRenderer renderer = new();
        renderer.Layout(tree, paneControls, x: 0, y: 0, width: 120, height: 40);

        SummaryLayoutNode cpuPane = tree.Panes().Single(p => p.ControlType == PaneControlType.Cpu);
        Control cpuControl = paneControls[cpuPane.Id];

        Assert.Equal(0, cpuControl.X);
        Assert.Equal(0, cpuControl.Y);
        Assert.Equal(40, cpuControl.Width);
        Assert.Equal(24, cpuControl.Height);
    }

    [Fact]
    public void Side_By_Side_Metres_Stack_As_One_Metre_High()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.FromNodes([
            Split(0, Orientation.Column, 0.5f, 1, 2), Split(1, Orientation.Row, 0.5f, 3, 4), Pane(2), Pane(3), Pane(4),
        ], rootId: 0);

        Dictionary<int, Rectangle> bounds = LayoutWithFixed(tree, [3, 4], height: 40);

        Assert.Equal(new Rectangle(0,  0,           60,  MetreHeight),      bounds[3]);
        Assert.Equal(new Rectangle(60, 0,           60,  MetreHeight),      bounds[4]);
        Assert.Equal(new Rectangle(0,  MetreHeight, 120, 40 - MetreHeight), bounds[2]);
    }

    private static SummaryLayoutNode Split(int id, Orientation orientation, float ratio, int firstId, int secondId) =>
        new() { Id = id, IsSplit = true, Orientation = orientation, Ratio = ratio, FirstId = firstId, SecondId = secondId };

    [Fact]
    public void Two_Stacked_Metres_Take_Their_Combined_Rows()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.FromNodes([
            Split(0, Orientation.Column, 0.5f, 1, 2), Split(1, Orientation.Column, 0.5f, 3, 4), Pane(2), Pane(3), Pane(4),
        ], rootId: 0);

        Dictionary<int, Rectangle> bounds = LayoutWithFixed(tree, [3, 4], height: 40);

        Assert.Equal(new Rectangle(0, 0,               120, MetreHeight),          bounds[3]);
        Assert.Equal(new Rectangle(0, MetreHeight,     120, MetreHeight),          bounds[4]);
        Assert.Equal(new Rectangle(0, MetreHeight * 2, 120, 40 - MetreHeight * 2), bounds[2]);
    }
}
