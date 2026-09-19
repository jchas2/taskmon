using System.Drawing;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

// Walks a SummaryLayoutTree and positions/sizes the Control instance behind each pane. Shared by
// SummaryControl2 (read-only display) and the Milestone 5 designer (interactive editing), so the
// designer is WYSIWYG by construction - the exact same code lays panes out in both places -
// rather than by carefully keeping two implementations in sync.
public sealed class SummaryLayoutRenderer
{
    private readonly Dictionary<int, Rectangle> paneBounds = new();

    // Every pane's last-computed screen rect, keyed by node id - used for spatial (nearest
    // neighbour in a direction) focus navigation between panes.
    public IReadOnlyDictionary<int, Rectangle> PaneBounds => paneBounds;

    public void Layout(
        SummaryLayoutTree tree,
        IReadOnlyDictionary<int, Control> paneControls,
        int x,
        int y,
        int width,
        int height)
    {
        paneBounds.Clear();
        LayoutNode(tree, paneControls, tree.RootId, x, y, width, height);
    }

    private void LayoutNode(
        SummaryLayoutTree tree,
        IReadOnlyDictionary<int, Control> paneControls,
        int nodeId,
        int x,
        int y,
        int width,
        int height)
    {
        SummaryLayoutNode node = tree.Nodes[nodeId];

        if (!node.IsSplit) {
            paneBounds[nodeId] = new Rectangle(x, y, width, height);

            if (paneControls.TryGetValue(nodeId, out Control? control)) {
                control.X = x;
                control.Y = y;
                control.Width = width;
                control.Height = height;
                control.Resize();
            }

            return;
        }

        // The second slot always takes width/height minus the first, rather than a second
        // ratio-derived value, so the two never leave (or overlap by) a stray rounded-off column.
        if (node.Orientation == Orientation.Row) {
            int firstWidth = (int)(width * node.Ratio);
            int secondWidth = width - firstWidth;

            LayoutNode(tree, paneControls, node.FirstId, x, y, firstWidth, height);
            LayoutNode(tree, paneControls, node.SecondId, x + firstWidth, y, secondWidth, height);
        }
        else {
            int firstHeight = (int)(height * node.Ratio);
            int secondHeight = height - firstHeight;

            LayoutNode(tree, paneControls, node.FirstId, x, y, width, firstHeight);
            LayoutNode(tree, paneControls, node.SecondId, x, y + firstHeight, width, secondHeight);
        }
    }
}
