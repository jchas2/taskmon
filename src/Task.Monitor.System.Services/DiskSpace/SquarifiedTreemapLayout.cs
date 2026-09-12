using System.Drawing;

namespace Task.Monitor.System.Services.DiskSpace;

// The squarified treemap algorithm (Bruls, Huizing, van Wijk 1999): lays out a set of weighted
// items inside a rectangle as nested, non-overlapping cells whose areas are proportional to their
// weights, choosing each row's membership to keep individual cell aspect ratios as close to
// square as it can - the property that makes a treemap read as legible blocks rather than the
// long, unreadable slivers a naive proportional split produces.
//
// Pure geometry: no I/O, no knowledge of the disk-space model, so it can be unit tested with
// plain numbers.
public static class SquarifiedTreemapLayout
{
    public static IReadOnlyList<TreemapCell> Layout(IReadOnlyList<TreemapItem> items, Rectangle bounds)
    {
        List<TreemapItem> positive = [.. items.Where(item => item.Weight > 0).OrderByDescending(item => item.Weight)];

        if (positive.Count == 0 || bounds.Width <= 0 || bounds.Height <= 0) {
            return [];
        }

        double totalWeight = positive.Sum(item => item.Weight);
        double totalArea = (double)bounds.Width * bounds.Height;
        double[] areas = [.. positive.Select(item => item.Weight / totalWeight * totalArea)];

        List<TreemapCell> results = [];
        SquarifyAreas(positive, areas, bounds, results);
        return results;
    }

    private static void SquarifyAreas(
        List<TreemapItem> items, double[] areas, Rectangle remaining, List<TreemapCell> results)
    {
        int index = 0;

        while (index < items.Count) {
            double sideLength = Math.Min(remaining.Width, remaining.Height);

            // The remaining space has collapsed to nothing (an artifact of integer rounding on a
            // very unequal weight distribution) - the leftover items simply aren't drawn rather
            // than producing a degenerate zero-size rectangle.
            if (sideLength <= 0) {
                return;
            }

            int rowEnd = index + 1;
            double rowArea = areas[index];
            double rowMin = areas[index];
            double rowMax = areas[index];

            while (rowEnd < items.Count) {
                double candidate = areas[rowEnd];
                double nextRowArea = rowArea + candidate;
                double nextMin = Math.Min(rowMin, candidate);
                double nextMax = Math.Max(rowMax, candidate);

                if (WorstRatio(nextRowArea, nextMin, nextMax, sideLength) >
                    WorstRatio(rowArea, rowMin, rowMax, sideLength)) {
                    break;
                }

                rowArea = nextRowArea;
                rowMin = nextMin;
                rowMax = nextMax;
                rowEnd++;
            }

            remaining = LayoutRow(items, areas, index, rowEnd, rowArea, remaining, results);
            index = rowEnd;
        }
    }

    // The worst (largest) aspect ratio any single cell in the row would have if the row's total
    // area were split proportionally along the given fixed side length. Lower is more square.
    private static double WorstRatio(double rowArea, double minArea, double maxArea, double sideLength)
    {
        if (rowArea <= 0) {
            return double.MaxValue;
        }

        double sideSquared = sideLength * sideLength;
        double rowAreaSquared = rowArea * rowArea;

        return Math.Max(
            sideSquared * maxArea / rowAreaSquared,
            rowAreaSquared / (sideSquared * minArea));
    }

    // Places items[start..end) as one strip against the shorter side of `remaining`, and returns
    // whatever space is left over for the next row.
    private static Rectangle LayoutRow(
        List<TreemapItem> items,
        double[] areas,
        int start,
        int end,
        double rowArea,
        Rectangle remaining,
        List<TreemapCell> results)
    {
        double sideLength = Math.Min(remaining.Width, remaining.Height);
        double thicknessExact = rowArea / sideLength;

        // A vertical strip (a column down the left edge) when the remaining space is wider than
        // it is tall - the strip's own thickness eats into Width and its items stack along Height.
        // Otherwise a horizontal strip along the top edge, stacking items along Width.
        bool vertical = remaining.Width >= remaining.Height;

        int thickness = Math.Max(1, (int)Math.Round(thicknessExact));
        thickness = Math.Min(thickness, vertical ? remaining.Width : remaining.Height);

        int stripLength = vertical ? remaining.Height : remaining.Width;

        double offset = 0;
        int previousBoundary = 0;

        for (int i = start; i < end; i++) {
            offset += areas[i] / thicknessExact;

            // The last item in the row always closes out exactly at stripLength, so rounding
            // error accumulated across the row doesn't leave a gap or overrun the strip.
            int boundary = i == end - 1 ? stripLength : (int)Math.Round(offset);
            int length = Math.Max(1, boundary - previousBoundary);

            Rectangle cellBounds = vertical
                ? new Rectangle(remaining.X, remaining.Y + previousBoundary, thickness, length)
                : new Rectangle(remaining.X + previousBoundary, remaining.Y, length, thickness);

            results.Add(new TreemapCell { Id = items[i].Id, Bounds = cellBounds });
            previousBoundary = boundary;
        }

        return vertical
            ? new Rectangle(remaining.X + thickness, remaining.Y, remaining.Width - thickness, remaining.Height)
            : new Rectangle(remaining.X, remaining.Y + thickness, remaining.Width, remaining.Height - thickness);
    }
}
