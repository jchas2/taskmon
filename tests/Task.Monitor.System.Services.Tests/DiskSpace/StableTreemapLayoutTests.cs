using System.Drawing;
using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.System.Services.Tests.DiskSpace;

public sealed class StableTreemapLayoutTests
{
    private static readonly Rectangle Bounds = new(0, 0, 40, 20);

    [Fact]
    public void First_Call_Computes_A_Fresh_Layout()
    {
        StableTreemapLayout layout = new();
        TreemapItem[] items = [
            new TreemapItem { Id = "A", Weight = 100 },
            new TreemapItem { Id = "B", Weight = 50 }
        ];

        IReadOnlyList<TreemapCell> cells = layout.Layout(items, Bounds);

        Assert.Equal(2, cells.Count);
    }

    [Fact]
    public void Small_Weight_Change_Under_Threshold_Returns_The_Same_Layout()
    {
        StableTreemapLayout layout = new(relativeChangeThreshold: 0.10);

        IReadOnlyList<TreemapCell> first = layout.Layout(
            [new TreemapItem { Id = "A", Weight = 100 }, new TreemapItem { Id = "B", Weight = 50 }],
            Bounds);

        // A 5% increase - below the 10% threshold - should not move anything.
        IReadOnlyList<TreemapCell> second = layout.Layout(
            [new TreemapItem { Id = "A", Weight = 105 }, new TreemapItem { Id = "B", Weight = 50 }],
            Bounds);

        Assert.Same(first, second);
    }

    [Fact]
    public void Weight_Change_Over_Threshold_Recomputes()
    {
        StableTreemapLayout layout = new(relativeChangeThreshold: 0.10);

        IReadOnlyList<TreemapCell> first = layout.Layout(
            [new TreemapItem { Id = "A", Weight = 100 }, new TreemapItem { Id = "B", Weight = 50 }],
            Bounds);

        // A 50% increase - well past the threshold - should trigger a fresh layout.
        IReadOnlyList<TreemapCell> second = layout.Layout(
            [new TreemapItem { Id = "A", Weight = 150 }, new TreemapItem { Id = "B", Weight = 50 }],
            Bounds);

        Assert.NotSame(first, second);

        Rectangle firstA = first.Single(c => c.Id == "A").Bounds;
        Rectangle secondA = second.Single(c => c.Id == "A").Bounds;
        Assert.NotEqual(firstA, secondA);
    }

    [Fact]
    public void Rank_Swap_Recomputes_Even_If_No_Single_Weight_Crossed_The_Threshold()
    {
        StableTreemapLayout layout = new(relativeChangeThreshold: 0.10);

        IReadOnlyList<TreemapCell> first = layout.Layout(
            [new TreemapItem { Id = "A", Weight = 100 }, new TreemapItem { Id = "B", Weight = 99 }],
            Bounds);

        // A and B swap rank without either moving by more than ~1% individually.
        IReadOnlyList<TreemapCell> second = layout.Layout(
            [new TreemapItem { Id = "A", Weight = 99 }, new TreemapItem { Id = "B", Weight = 100 }],
            Bounds);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Item_Count_Change_Recomputes()
    {
        StableTreemapLayout layout = new();

        IReadOnlyList<TreemapCell> first = layout.Layout(
            [new TreemapItem { Id = "A", Weight = 100 }],
            Bounds);

        IReadOnlyList<TreemapCell> second = layout.Layout(
            [new TreemapItem { Id = "A", Weight = 100 }, new TreemapItem { Id = "B", Weight = 50 }],
            Bounds);

        Assert.NotSame(first, second);
        Assert.Equal(2, second.Count);
    }

    [Fact]
    public void Bounds_Change_Always_Recomputes()
    {
        StableTreemapLayout layout = new();
        TreemapItem[] items = [new TreemapItem { Id = "A", Weight = 100 }];

        IReadOnlyList<TreemapCell> first = layout.Layout(items, Bounds);
        IReadOnlyList<TreemapCell> second = layout.Layout(items, new Rectangle(0, 0, 20, 10));

        Assert.NotSame(first, second);
        Assert.Equal(new Rectangle(0, 0, 20, 10), second.Single().Bounds);
    }
}
