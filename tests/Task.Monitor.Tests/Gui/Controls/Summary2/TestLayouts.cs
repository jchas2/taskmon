using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Summary2.Layout;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

// Layouts the tests share, kept apart from the app's own so changing those can't move a test's
// panes. Example() is Column(Row(Cpu[3], Row(Memory[5], Gpu[6])[4])[1], Process[2])[0]: CPU,
// memory and GPU charts side by side over the process list, rooted at node 0.
internal static class TestLayouts
{
    public static LayoutTree<SummaryPane> Example() =>
        LayoutTree<SummaryPane>.FromNodes([
            new LayoutNode<SummaryPane> { Id = 0, IsSplit = true, Orientation = Orientation.Column, Ratio = 0.6f, FirstId = 1, SecondId = 2 },
            new LayoutNode<SummaryPane> { Id = 1, IsSplit = true, Orientation = Orientation.Row, Ratio = 1f / 3f, FirstId = 3, SecondId = 4 },
            new LayoutNode<SummaryPane> { Id = 2, Pane = new(PaneControlType.Process, Statistics.Process | Statistics.Pid | Statistics.Cpu | Statistics.Mem) },
            new LayoutNode<SummaryPane> { Id = 3, Pane = new(PaneControlType.Cpu) },
            new LayoutNode<SummaryPane> { Id = 4, IsSplit = true, Orientation = Orientation.Row, Ratio = 0.5f, FirstId = 5, SecondId = 6 },
            new LayoutNode<SummaryPane> { Id = 5, Pane = new(PaneControlType.Memory) },
            new LayoutNode<SummaryPane> { Id = 6, Pane = new(PaneControlType.Gpu) },
        ], rootId: 0);
}
