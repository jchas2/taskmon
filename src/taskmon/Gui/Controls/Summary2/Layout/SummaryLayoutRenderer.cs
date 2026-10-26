using System.Drawing;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public sealed class SummaryLayoutRenderer
{
    private readonly Dictionary<int, Rectangle> paneBounds = new();

    // The nearest split whose ratio changes the pane's size. A stacked split with a fixed-height side
    // ignores its ratio, and no stacked split can change a fixed-height pane, but a side-by-side split
    // above either still sets the width.
    public static (int SplitId, bool InFirst)? FindResizableSplit(
        SummaryLayoutTree                 tree,
        IReadOnlyDictionary<int, Control> paneControls,
        int                               paneId)
    {
        bool paneIsFixed = FixedHeight(tree, paneControls, paneId) is not null;
        int  childId     = paneId;

        while (tree.FindParentSplitId(childId) is { } parentId) {
            SummaryLayoutNode parent = tree.Nodes[parentId];

            bool ratioApplies = parent.Orientation == Orientation.Row ||
                (!paneIsFixed &&
                 FixedHeight(tree, paneControls, parent.FirstId) is null &&
                 FixedHeight(tree, paneControls, parent.SecondId) is null);

            if (ratioApplies) {
                return (parentId, parent.FirstId == childId);
            }

            childId = parentId;
        }

        return null;
    }

    private static int FirstHeight(
        SummaryLayoutTree                 tree,
        IReadOnlyDictionary<int, Control> paneControls,
        SummaryLayoutNode                 split,
        int                               height)
    {
        int? first  = FixedHeight(tree, paneControls, split.FirstId);
        int? second = FixedHeight(tree, paneControls, split.SecondId);

        return (first, second) switch {
            ({ } fixedFirst, _)     => Math.Min(fixedFirst, height),
            (null, { } fixedSecond) => height - Math.Min(fixedSecond, height),
                                  _ => (int)(height * split.Ratio)
        };
    }

    public static int? FixedHeight(SummaryLayoutTree tree, IReadOnlyDictionary<int, Control> paneControls, int nodeId)
    {
        SummaryLayoutNode node = tree.Nodes[nodeId];

        if (!node.IsSplit) {
            return paneControls.TryGetValue(nodeId, out Control? control) && control is IFixedHeightPane pane
                ? pane.FixedHeight
                : null;
        }

        if (FixedHeight(tree, paneControls, node.FirstId) is not { } first ||
            FixedHeight(tree, paneControls, node.SecondId) is not { } second) {
            return null;
        }

        return node.Orientation == Orientation.Column
            ? first + second
            : Math.Max(first, second);
    }

    public void Layout(
        SummaryLayoutTree                 tree,
        IReadOnlyDictionary<int, Control> paneControls,
        int                               x,
        int                               y,
        int                               width,
        int                               height)
    {
        paneBounds.Clear();
        
        LayoutNode(
            tree, 
            paneControls, 
            tree.RootId, 
            x, 
            y, 
            width, 
            height);
    }

    private void LayoutNode(
        SummaryLayoutTree                 tree,
        IReadOnlyDictionary<int, Control> paneControls,
        int                               nodeId,
        int                               x,
        int                               y,
        int                               width,
        int                               height)
    {
        SummaryLayoutNode node = tree.Nodes[nodeId];

        if (!node.IsSplit) {
            paneBounds[nodeId] = new Rectangle(x, y, width, height);

            if (paneControls.TryGetValue(nodeId, out Control? control)) {
                control.X      = x;
                control.Y      = y;
                control.Width  = width;
                control.Height = height;
                control.Resize();
            }

            return;
        }

        if (node.Orientation == Orientation.Row) {
            int firstWidth  = (int)(width * node.Ratio);
            int secondWidth = width - firstWidth;

            LayoutNode(
                tree, 
                paneControls, 
                node.FirstId, 
                x, 
                y, 
                firstWidth, 
                height);
            
            LayoutNode(
                tree, 
                paneControls, 
                node.SecondId, 
                x + firstWidth, 
                y, 
                secondWidth, 
                height);
        }
        else {
            int firstHeight  = FirstHeight(tree, paneControls, node, height);
            int secondHeight = height - firstHeight;

            LayoutNode(
                tree, 
                paneControls, 
                node.FirstId, 
                x, 
                y, 
                width, 
                firstHeight);
            
            LayoutNode(
                tree, 
                paneControls, 
                node.SecondId, 
                x, 
                y + firstHeight, 
                width, 
                secondHeight);
        }
    }

    // Every pane's last-computed screen rect, keyed by node id - used for nearest
    // neighbour focus navigation between panes.
    public IReadOnlyDictionary<int, Rectangle> PaneBounds => paneBounds;
}
