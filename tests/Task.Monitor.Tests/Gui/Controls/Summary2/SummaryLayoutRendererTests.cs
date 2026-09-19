using System.Drawing;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

// Confirms the renderer's recursive split math against SummaryLayoutTree.CreateExample()'s shape:
// a row of three panes (Cpu | Memory | Gpu) over one Process pane, nested as
// Column(Row(Cpu, Row(Memory, Gpu)), Process).
public sealed class SummaryLayoutRendererTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public SummaryLayoutRendererTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
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
}
