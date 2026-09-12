using System.Drawing;
using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.System.Services.Tests.DiskSpace;

public sealed class SquarifiedTreemapLayoutTests
{
    [Fact]
    public void Single_Item_Fills_The_Entire_Bounds()
    {
        Rectangle bounds = new(2, 3, 10, 5);
        TreemapItem[] items = [new TreemapItem { Id = "A", Weight = 1 }];

        IReadOnlyList<TreemapCell> cells = SquarifiedTreemapLayout.Layout(items, bounds);

        TreemapCell cell = Assert.Single(cells);
        Assert.Equal("A", cell.Id);
        Assert.Equal(bounds, cell.Bounds);
    }

    [Fact]
    public void Two_Equal_Weights_In_A_Wide_Rectangle_Split_Into_Two_Square_Halves()
    {
        Rectangle bounds = new(0, 0, 20, 10);
        TreemapItem[] items = [
            new TreemapItem { Id = "A", Weight = 1 },
            new TreemapItem { Id = "B", Weight = 1 }
        ];

        IReadOnlyList<TreemapCell> cells = SquarifiedTreemapLayout.Layout(items, bounds);

        Assert.Equal(2, cells.Count);
        Assert.Equal(new Rectangle(0, 0, 10, 10), cells.Single(c => c.Id == "A").Bounds);
        Assert.Equal(new Rectangle(10, 0, 10, 10), cells.Single(c => c.Id == "B").Bounds);
    }

    [Fact]
    public void Zero_And_Negative_Weight_Items_Are_Excluded()
    {
        Rectangle bounds = new(0, 0, 10, 10);
        TreemapItem[] items = [
            new TreemapItem { Id = "A", Weight = 1 },
            new TreemapItem { Id = "Zero", Weight = 0 },
            new TreemapItem { Id = "Negative", Weight = -5 }
        ];

        IReadOnlyList<TreemapCell> cells = SquarifiedTreemapLayout.Layout(items, bounds);

        Assert.Equal(["A"], cells.Select(c => c.Id));
    }

    [Fact]
    public void Empty_Items_Produces_No_Cells()
    {
        Assert.Empty(SquarifiedTreemapLayout.Layout([], new Rectangle(0, 0, 10, 10)));
    }

    [Fact]
    public void Zero_Size_Bounds_Produces_No_Cells()
    {
        TreemapItem[] items = [new TreemapItem { Id = "A", Weight = 1 }];

        Assert.Empty(SquarifiedTreemapLayout.Layout(items, new Rectangle(0, 0, 0, 10)));
        Assert.Empty(SquarifiedTreemapLayout.Layout(items, new Rectangle(0, 0, 10, 0)));
    }

    [Fact]
    public void Every_Item_Appears_Exactly_Once()
    {
        Rectangle bounds = new(0, 0, 40, 20);
        TreemapItem[] items = [
            new TreemapItem { Id = "A", Weight = 100 },
            new TreemapItem { Id = "B", Weight = 60 },
            new TreemapItem { Id = "C", Weight = 40 },
            new TreemapItem { Id = "D", Weight = 25 },
            new TreemapItem { Id = "E", Weight = 10 },
            new TreemapItem { Id = "F", Weight = 5 }
        ];

        IReadOnlyList<TreemapCell> cells = SquarifiedTreemapLayout.Layout(items, bounds);

        Assert.Equal(items.Select(i => i.Id).OrderBy(id => id), cells.Select(c => c.Id).OrderBy(id => id));
    }

    [Fact]
    public void Cells_Do_Not_Overlap_And_Stay_Within_Bounds()
    {
        Rectangle bounds = new(0, 0, 40, 20);
        TreemapItem[] items = [
            new TreemapItem { Id = "A", Weight = 100 },
            new TreemapItem { Id = "B", Weight = 60 },
            new TreemapItem { Id = "C", Weight = 40 },
            new TreemapItem { Id = "D", Weight = 25 },
            new TreemapItem { Id = "E", Weight = 10 },
            new TreemapItem { Id = "F", Weight = 5 }
        ];

        IReadOnlyList<TreemapCell> cells = SquarifiedTreemapLayout.Layout(items, bounds);

        foreach (TreemapCell cell in cells) {
            Assert.True(bounds.Contains(cell.Bounds), $"{cell.Id} escaped bounds: {cell.Bounds}");
        }

        for (int i = 0; i < cells.Count; i++) {
            for (int j = i + 1; j < cells.Count; j++) {
                Assert.False(
                    cells[i].Bounds.IntersectsWith(cells[j].Bounds),
                    $"{cells[i].Id} overlaps {cells[j].Id}: {cells[i].Bounds} vs {cells[j].Bounds}");
            }
        }
    }

    [Fact]
    public void Total_Cell_Area_Approximately_Equals_Bounds_Area()
    {
        Rectangle bounds = new(0, 0, 37, 23);
        TreemapItem[] items = [
            new TreemapItem { Id = "A", Weight = 91 },
            new TreemapItem { Id = "B", Weight = 47 },
            new TreemapItem { Id = "C", Weight = 33 },
            new TreemapItem { Id = "D", Weight = 21 },
            new TreemapItem { Id = "E", Weight = 13 },
            new TreemapItem { Id = "F", Weight = 7 },
            new TreemapItem { Id = "G", Weight = 3 }
        ];

        IReadOnlyList<TreemapCell> cells = SquarifiedTreemapLayout.Layout(items, bounds);

        long totalCellArea = cells.Sum(c => (long)c.Bounds.Width * c.Bounds.Height);
        long boundsArea = (long)bounds.Width * bounds.Height;

        // Rounding each cell's boundary to a whole character independently can drift the total by
        // a handful of cells on a small grid; a tolerance proportional to the item count covers
        // that without hiding a real layout bug.
        Assert.InRange(totalCellArea, boundsArea - items.Length * 2, boundsArea);
    }
}
