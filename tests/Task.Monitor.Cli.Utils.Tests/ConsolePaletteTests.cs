using System.Drawing;

namespace Task.Monitor.Cli.Utils.Tests;

public sealed class ConsolePaletteTests
{
    private const char Esc = (char)27;

    private static readonly Color Fallback = Color.FromArgb(255, 1, 2, 3);

    [Theory]
    [InlineData("#FF8800",   255, 0xFF, 0x88, 0x00)]
    [InlineData("FF8800",    255, 0xFF, 0x88, 0x00)]
    [InlineData("#000000",   255, 0, 0, 0)]
    [InlineData("#80FF8800", 0x80, 0xFF, 0x88, 0x00)]
    public void FromHex_Parses_Hex(string value, int a, int r, int g, int b)
    {
        Color colour = ConsolePalette.FromHex(value, Fallback);

        Assert.Equal(a, colour.A);
        Assert.Equal(r, colour.R);
        Assert.Equal(g, colour.G);
        Assert.Equal(b, colour.B);
    }

    [Theory]
    [InlineData("transparent")]
    [InlineData("none")]
    [InlineData("#00000000")]
    public void FromHex_Parses_Transparent_As_Alpha_Zero(string value)
    {
        Color colour = ConsolePalette.FromHex(value, Fallback);

        Assert.Equal(0, colour.A);
        Assert.True(colour.IsTransparent());
    }

    [Theory]
    [InlineData("DarkBlue")]
    [InlineData("darkblue")]
    public void FromHex_Falls_Back_To_Legacy_Colour_Names(string value)
    {
        Color colour = ConsolePalette.FromHex(value, Fallback);

        Assert.Equal(ConsolePalette.DarkBlue.ToArgb(), colour.ToArgb());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-colour")]
    [InlineData("#12")]
    public void FromHex_Returns_Fallback_For_Invalid_Input(string? value)
    {
        Color colour = ConsolePalette.FromHex(value, Fallback);

        Assert.Equal(Fallback.ToArgb(), colour.ToArgb());
    }

    [Fact]
    public void ToHex_Emits_Opaque_As_RRGGBB()
    {
        Assert.Equal("#FF8800", ConsolePalette.ToHex(Color.FromArgb(255, 0xFF, 0x88, 0x00)));
    }

    [Fact]
    public void ToHex_Emits_Transparent_Token_For_Alpha_Zero()
    {
        Assert.Equal("transparent", ConsolePalette.ToHex(ConsolePalette.Transparent));
    }

    [Fact]
    public void ToHex_Emits_AARRGGBB_For_Partial_Alpha()
    {
        Assert.Equal("#80FF8800", ConsolePalette.ToHex(Color.FromArgb(0x80, 0xFF, 0x88, 0x00)));
    }

    [Theory]
    [InlineData("#FF8800")]
    [InlineData("transparent")]
    [InlineData("#80FF8800")]
    public void ToHex_FromHex_RoundTrips(string value)
    {
        Color colour = ConsolePalette.FromHex(value, Fallback);
        
        Assert.Equal(value, ConsolePalette.ToHex(colour));
    }

    [Fact]
    public void BackgroundSgr_Emits_TrueColour_When_Opaque()
    {
        Assert.Equal(Esc + "[48;2;255;136;0m", ConsolePalette.BackgroundSgr(Color.FromArgb(255, 255, 136, 0)));
    }

    [Fact]
    public void BackgroundSgr_Emits_Default_When_Transparent()
    {
        Assert.Equal(Esc + "[49m", ConsolePalette.BackgroundSgr(ConsolePalette.Transparent));
    }

    [Fact]
    public void ForegroundSgr_Emits_TrueColour_When_Opaque()
    {
        Assert.Equal(Esc + "[38;2;255;136;0m", ConsolePalette.ForegroundSgr(Color.FromArgb(255, 255, 136, 0)));
    }

    [Fact]
    public void ForegroundSgr_Emits_Default_When_Transparent()
    {
        Assert.Equal(Esc + "[39m", ConsolePalette.ForegroundSgr(ConsolePalette.Transparent));
    }

    public static IEnumerable<object[]> StandardColours()
    {
        yield return new object[] { ConsolePalette.Black,       0 };
        yield return new object[] { ConsolePalette.DarkRed,     1 };
        yield return new object[] { ConsolePalette.DarkGreen,   2 };
        yield return new object[] { ConsolePalette.DarkYellow,  3 };
        yield return new object[] { ConsolePalette.DarkBlue,    4 };
        yield return new object[] { ConsolePalette.DarkMagenta, 5 };
        yield return new object[] { ConsolePalette.DarkCyan,    6 };
        yield return new object[] { ConsolePalette.Gray,        7 };
        yield return new object[] { ConsolePalette.DarkGray,    8 };
        yield return new object[] { ConsolePalette.Red,         9 };
        yield return new object[] { ConsolePalette.Green,       10 };
        yield return new object[] { ConsolePalette.Yellow,      11 };
        yield return new object[] { ConsolePalette.Blue,        12 };
        yield return new object[] { ConsolePalette.Magenta,     13 };
        yield return new object[] { ConsolePalette.Cyan,        14 };
        yield return new object[] { ConsolePalette.White,       15 };
    }

    [Theory]
    [MemberData(nameof(StandardColours))]
    public void TryGetAnsiIndex_Maps_Each_Standard_Colour(Color colour, int expectedIndex)
    {
        Assert.True(ConsolePalette.TryGetAnsiIndex(colour, out int index));
        Assert.Equal(expectedIndex, index);
    }

    [Fact]
    public void TryGetAnsiIndex_Fails_For_NonPalette_Colour()
    {
        Assert.False(ConsolePalette.TryGetAnsiIndex(Color.FromArgb(255, 10, 20, 30), out _));
    }

    [Fact]
    public void TryGetAnsiIndex_Fails_For_Transparent()
    {
        Assert.False(ConsolePalette.TryGetAnsiIndex(ConsolePalette.Transparent, out _));
    }

    [Theory]
    [MemberData(nameof(StandardColours))]
    public void ForegroundSgr_Emits_Indexed_Code_When_Preferred(Color colour, int index)
    {
        int expected = index < 8 ? 30 + index : 90 + (index - 8);
        
        WithIndexedColours(() =>
            Assert.Equal(Esc + "[" + expected + "m", ConsolePalette.ForegroundSgr(colour)));
    }

    [Theory]
    [MemberData(nameof(StandardColours))]
    public void BackgroundSgr_Emits_Indexed_Code_When_Preferred(Color colour, int index)
    {
        int expected = index < 8 ? 40 + index : 100 + (index - 8);
        
        WithIndexedColours(() =>
            Assert.Equal(Esc + "[" + expected + "m", ConsolePalette.BackgroundSgr(colour)));
    }

    [Fact]
    public void ForegroundSgr_Stays_TrueColour_For_NonPalette_Colour_When_Preferred()
    {
        WithIndexedColours(() =>
            Assert.Equal(
                Esc + "[38;2;255;136;0m",
                ConsolePalette.ForegroundSgr(Color.FromArgb(255, 255, 136, 0))));
    }

    [Fact]
    public void ForegroundSgr_Stays_TrueColour_For_Palette_Colour_When_Not_Preferred()
    {
        // Default (true colour) behaviour must not emit indexed codes from the palette.
        Assert.Equal(Esc + "[38;2;0;0;255m", ConsolePalette.ForegroundSgr(ConsolePalette.Blue));
    }

    private static void WithIndexedColours(Action action)
    {
        bool previous = ConsolePalette.PreferIndexedColours;
        ConsolePalette.PreferIndexedColours = true;

        try {
            action();
        }
        finally {
            ConsolePalette.PreferIndexedColours = previous;
        }
    }
}
