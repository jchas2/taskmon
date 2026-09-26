using System.Drawing;

namespace Task.Monitor.System.Services.DiskSpace;

// Implements the squarified treemap algo (Bruls, Huizing, van Wijk 1999).
public static class SquarifiedTreemapLayout
{
    public static IReadOnlyList<TreemapCell> Layout(IReadOnlyList<TreemapItem> items, Rectangle bounds)
    {
        List<TreemapItem> positive = [.. items
            .Where(item => item.Weight > 0)
            .OrderByDescending(item => item.Weight)];

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
        List<TreemapItem> items, 
        double[] areas, 
        Rectangle remaining, 
        List<TreemapCell> results)
    {
        int index = 0;

        while (index < items.Count) {
            double sideLength = Math.Min(remaining.Width, remaining.Height);

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

                if (WorstRatio(
                        nextRowArea, 
                        nextMin, 
                        nextMax, 
                        sideLength) >
                    WorstRatio(
                        rowArea, 
                        rowMin, 
                        rowMax, 
                        sideLength)) {
                    
                    break;
                }

                rowArea = nextRowArea;
                rowMin = nextMin;
                rowMax = nextMax;
                rowEnd++;
            }

            remaining = LayoutRow(
                items, 
                areas, 
                index, 
                rowEnd, 
                rowArea, 
                remaining, 
                results);
            
            index = rowEnd;
        }
    }

    private static double WorstRatio(
        double rowArea, 
        double minArea, 
        double maxArea, 
        double sideLength)
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

        bool vertical = remaining.Width >= remaining.Height;

        int thickness = Math.Max(1, (int)Math.Round(thicknessExact));
        
        thickness = Math.Min(thickness, vertical 
            ? remaining.Width 
            : remaining.Height);

        int stripLength = vertical 
            ? remaining.Height 
            : remaining.Width;

        double offset = 0;
        int previousBoundary = 0;

        for (int i = start; i < end; i++) {
            offset += areas[i] / thicknessExact;

            int itemsRemainingAfter = end - 1 - i;
            
            int boundary = i == end - 1
                ? stripLength
                : Math.Min((int)Math.Round(offset), stripLength - itemsRemainingAfter);
            
            int length = Math.Max(1, boundary - previousBoundary);

            Rectangle cellBounds = vertical
                ? new Rectangle(
                    remaining.X, 
                    remaining.Y + previousBoundary, 
                    thickness, 
                    length)
                : new Rectangle(
                    remaining.X + previousBoundary, 
                    remaining.Y, 
                    length, 
                    thickness);

            results.Add(new TreemapCell {
                Id = items[i].Id, 
                Bounds = cellBounds
            });
            
            previousBoundary = boundary;
        }

        return vertical
            ? new Rectangle(
                remaining.X + thickness, 
                remaining.Y, 
                remaining.Width - thickness, 
                remaining.Height)
            : new Rectangle(
                remaining.X, 
                remaining.Y + thickness, 
                remaining.Width, 
                remaining.Height - thickness);
    }
}
