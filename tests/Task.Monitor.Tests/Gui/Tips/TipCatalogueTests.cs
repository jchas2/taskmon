using Task.Monitor.Gui.Tips;

namespace Task.Monitor.Tests.Gui.Tips;

public sealed class TipCatalogueTests
{
    // A footer on a typical 120-column terminal has room for this after "● Tip: " and the
    // next-tip hint. A longer tip is cut short, so fail here rather than in front of a user.
    private const int MaxVisibleLength = 100;

    [Fact]
    public void Every_Tip_Fits_The_Footer()
    {
        foreach (string tip in TipCatalogue.Tips) {
            int length = TipCatalogue.VisibleText(tip).Length;
            Assert.True(length <= MaxVisibleLength, $"{length} characters: {tip}");
        }
    }

    [Fact]
    public void Every_Tip_Has_Balanced_Key_Markers()
    {
        foreach (string tip in TipCatalogue.Tips) {
            int depth = 0;

            foreach (char ch in tip) {
                depth += ch switch { '{' => 1, '}' => -1, _ => 0 };
                Assert.InRange(depth, 0, 1);
            }

            Assert.Equal(0, depth);
        }
    }

    [Fact]
    public void Segments_Mark_Keys_And_Drop_The_Braces()
    {
        (string Text, bool IsKey)[] segments = TipCatalogue.Segments("Press {z} or {F5}.").ToArray();

        Assert.Equal(
            [("Press ", false), ("z", true), (" or ", false), ("F5", true), (".", false)],
            segments);
    }

    [Fact]
    public void Segments_Of_A_Tip_Starting_With_A_Key_Have_No_Empty_Text()
    {
        Assert.Equal([("t", true), (" next", false)], TipCatalogue.Segments("{t} next").ToArray());
    }

    [Fact]
    public void There_Are_Tips_And_None_Are_Empty_Or_Repeated()
    {
        Assert.NotEmpty(TipCatalogue.Tips);
        Assert.All(TipCatalogue.Tips, tip => Assert.False(string.IsNullOrWhiteSpace(tip)));
        Assert.Equal(TipCatalogue.Tips.Count, TipCatalogue.Tips.Distinct().Count());
    }

    [Fact]
    public void VisibleText_Drops_The_Braces()
    {
        Assert.Equal("Press z to freeze.", TipCatalogue.VisibleText("Press {z} to freeze."));
    }
}
