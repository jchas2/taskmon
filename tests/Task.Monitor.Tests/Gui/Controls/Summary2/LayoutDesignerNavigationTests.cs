using System.Drawing;
using Task.Monitor.Gui;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

// The layout designer moves its selection with the arrow keys to the nearest pane that way. A 2x2 grid of panes, ids matching their screen position: TopLeft(1) TopRight(2) / BottomLeft(3)
// BottomRight(4) - deliberately simple bounds so each direction's expected answer is unambiguous.
public sealed class LayoutDesignerNavigationTests
{
    [Fact]
    public void Down_From_TopLeft_Finds_BottomLeft()
    {
        Assert.Equal(3, LayoutDesignerScreen.FindNearest(Grid(), 1, ConsoleKey.DownArrow));
    }

    private static Dictionary<int, Rectangle> Grid() => new() {
        [1] = new Rectangle(0, 0, 50, 20),
        [2] = new Rectangle(50, 0, 50, 20),
        [3] = new Rectangle(0, 20, 50, 20),
        [4] = new Rectangle(50, 20, 50, 20),
    };

    [Fact]
    public void Left_From_BottomRight_Finds_BottomLeft()
    {
        Assert.Equal(3, LayoutDesignerScreen.FindNearest(Grid(), 4, ConsoleKey.LeftArrow));
    }

    [Fact]
    public void Picks_The_Nearest_Of_Several_Candidates_In_The_Same_Direction()
    {
        Dictionary<int, Rectangle> bounds = new() {
            [1] = new Rectangle(0, 0, 20, 20),
            [2] = new Rectangle(20, 0, 20, 20),   // adjacent
            [3] = new Rectangle(40, 0, 20, 20),   // further right
        };

        Assert.Equal(2, LayoutDesignerScreen.FindNearest(bounds, 1, ConsoleKey.RightArrow));
    }

    [Fact]
    public void Returns_Null_For_An_Unknown_Starting_Id()
    {
        Assert.Null(LayoutDesignerScreen.FindNearest(Grid(), 999, ConsoleKey.RightArrow));
    }

    [Fact]
    public void Returns_Null_When_Nothing_Lies_In_That_Direction()
    {
        Assert.Null(LayoutDesignerScreen.FindNearest(Grid(), 1, ConsoleKey.LeftArrow));
        Assert.Null(LayoutDesignerScreen.FindNearest(Grid(), 1, ConsoleKey.UpArrow));
    }

    [Fact]
    public void Right_From_TopLeft_Finds_TopRight()
    {
        Assert.Equal(2, LayoutDesignerScreen.FindNearest(Grid(), 1, ConsoleKey.RightArrow));
    }

    [Fact]
    public void Ties_On_Gap_Break_By_Greatest_Perpendicular_Overlap()
    {
        // Two panes both directly to the right at the same horizontal gap; the one that overlaps
        // the source pane's vertical span more should win.
        Dictionary<int, Rectangle> bounds = new() {
            [1] = new Rectangle(0, 0, 20, 40),     // y: 0-40
            [2] = new Rectangle(20, 0, 20, 10),    // y: 0-10  - overlap 10
            [3] = new Rectangle(20, 0, 20, 30),    // y: 0-30  - overlap 30, wins
        };

        Assert.Equal(3, LayoutDesignerScreen.FindNearest(bounds, 1, ConsoleKey.RightArrow));
    }

    [Fact]
    public void Up_From_BottomRight_Finds_TopRight()
    {
        Assert.Equal(2, LayoutDesignerScreen.FindNearest(Grid(), 4, ConsoleKey.UpArrow));
    }
}
