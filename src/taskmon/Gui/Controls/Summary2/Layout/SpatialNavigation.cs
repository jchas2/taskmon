using System.Drawing;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public enum SpatialDirection { Left, Right, Up, Down }

// Standard tiling-window-manager neighbour search: among panes whose rect lies in the given
// direction from a starting pane, picks the one with the smallest gap, tie-broken by whichever
// overlaps the starting pane most along the perpendicular axis. Pulled out as a standalone helper
// (rather than living on SummaryControl2, which cycles panes in tree order instead - see its
// OnKeyPressed) because LayoutDesignerScreen's selection genuinely is a 2D cursor moving around a
// visual layout, where "the pane above/below/left/right on screen" is the only sensible meaning
// of an arrow key, unlike a linear left/right tab order between content panes.
public static class SpatialNavigation
{
    public static int? FindNearest(
        IReadOnlyDictionary<int, Rectangle> paneBounds,
        int fromId,
        SpatialDirection direction)
    {
        if (!paneBounds.TryGetValue(fromId, out Rectangle from)) {
            return null;
        }

        int? bestId = null;
        long bestGap = long.MaxValue;
        long bestOverlap = -1;

        foreach ((int candidateId, Rectangle to) in paneBounds) {
            if (candidateId == fromId) {
                continue;
            }

            long gap;
            long overlap;

            switch (direction) {
                case SpatialDirection.Left:
                    if (to.Right > from.Left) continue;
                    gap = from.Left - to.Right;
                    overlap = OverlapY(from, to);
                    break;
                case SpatialDirection.Right:
                    if (to.Left < from.Right) continue;
                    gap = to.Left - from.Right;
                    overlap = OverlapY(from, to);
                    break;
                case SpatialDirection.Up:
                    if (to.Bottom > from.Top) continue;
                    gap = from.Top - to.Bottom;
                    overlap = OverlapX(from, to);
                    break;
                default:
                    if (to.Top < from.Bottom) continue;
                    gap = to.Top - from.Bottom;
                    overlap = OverlapX(from, to);
                    break;
            }

            if (overlap <= 0) {
                continue;
            }

            if (gap < bestGap || (gap == bestGap && overlap > bestOverlap)) {
                bestGap = gap;
                bestOverlap = overlap;
                bestId = candidateId;
            }
        }

        return bestId;
    }

    private static long OverlapX(Rectangle a, Rectangle b) =>
        Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);

    private static long OverlapY(Rectangle a, Rectangle b) =>
        Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
}
