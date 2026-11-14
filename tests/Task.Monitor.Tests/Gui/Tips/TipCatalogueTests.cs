using System.Globalization;
using Task.Monitor.Gui.Tips;
using Task.Monitor.Resources;

namespace Task.Monitor.Tests.Gui.Tips;

public sealed class TipCatalogueTests
{
    // A footer on a typical 120-column terminal has room for this after "● Tip: " and the
    // next-tip hint. A longer tip is cut short, so fail here rather than in front of a user.
    private const int MaxVisibleLength = 100;

    // A longer "Tip:" label or next-tip hint in another language leaves less room for the tip.
    [Theory]
    [InlineData(UiCulture.British)]
    [InlineData(UiCulture.American)]
    [InlineData(UiCulture.BrazilianPortuguese)]
    [InlineData(UiCulture.Danish)]
    [InlineData(UiCulture.Dutch)]
    [InlineData(UiCulture.French)]
    [InlineData(UiCulture.German)]
    [InlineData(UiCulture.Italian)]
    [InlineData(UiCulture.Polish)]
    [InlineData(UiCulture.Spanish)]
    public void Every_Tip_Fits_The_Footer(string cultureName)
    {
        CultureInfo british = CultureInfo.GetCultureInfo(UiCulture.British);
        int         room    = MaxVisibleLength;

        using IDisposable _ = UiCulture.Use(cultureName);

        room -= Strings.FooterTip.Length - Strings.ResourceManager.GetString(nameof(Strings.FooterTip), british)!.Length;
        room -= Strings.FooterKeyNextTip.Length - Strings.ResourceManager.GetString(nameof(Strings.FooterKeyNextTip), british)!.Length;

        foreach (string tip in TipCatalogue.Tips) {
            int length = TipCatalogue.VisibleText(tip).Length;
            Assert.True(length <= room, $"{cultureName}: {length} characters, room for {room}: {tip}");
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
