using System.Drawing;

namespace Task.Monitor.System.Services.DiskSpace;

// Wraps SquarifiedTreemapLayout to stop the heat map reflowing on every publish as sizes tick up
// during a live scan. 
public sealed class StableTreemapLayout
{
    private const double DefaultRelativeChangeThreshold = 0.10;

    private readonly double relativeChangeThreshold;

    private IReadOnlyList<TreemapItem> previousItems = [];
    private Rectangle previousBounds;
    private IReadOnlyList<TreemapCell> previousLayout = [];

    public StableTreemapLayout(double relativeChangeThreshold = DefaultRelativeChangeThreshold) =>
        this.relativeChangeThreshold = relativeChangeThreshold;

    public IReadOnlyList<TreemapCell> Layout(IReadOnlyList<TreemapItem> items, Rectangle bounds)
    {
        if (bounds != previousBounds || !IsStableRelativeToPrevious(items)) {
            previousLayout = SquarifiedTreemapLayout.Layout(items, bounds);
            previousItems = items;
            previousBounds = bounds;
        }

        return previousLayout;
    }

    private bool IsStableRelativeToPrevious(IReadOnlyList<TreemapItem> items)
    {
        if (items.Count != previousItems.Count) {
            return false;
        }

        List<TreemapItem> currentOrdered = [.. items.OrderByDescending(item => item.Weight)];
        List<TreemapItem> previousOrdered = [.. previousItems.OrderByDescending(item => item.Weight)];

        for (int i = 0; i < currentOrdered.Count; i++) {
            TreemapItem current = currentOrdered[i];
            TreemapItem previous = previousOrdered[i];

            if (current.Id != previous.Id) {
                return false;
            }

            double baseline = Math.Max(previous.Weight, 1.0);

            if (Math.Abs(current.Weight - previous.Weight) / baseline > relativeChangeThreshold) {
                return false;
            }
        }

        return true;
    }
}
