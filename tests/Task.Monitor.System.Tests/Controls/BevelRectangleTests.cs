using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls;

namespace Task.Monitor.System.Tests.Controls;

// Control.DrawingCapabilities is a process-wide static; each test sets it and Dispose
// puts back the default.
public sealed class BevelRectangleTests : IDisposable
{
    private const string QuadrantGlyphs = "▖▗▘▝▞";

    // The block glyphs that reach the top of a cell, which Terminal.app leaves a seam above.
    private const string TopReachingGlyphs = "▀▌▐█▘▝▞";

    private const DrawingCapabilities AppleTerminal = DrawingCapabilities.QuadrantGlyphs;
    private const DrawingCapabilities Conhost       = DrawingCapabilities.SeamlessBlockGlyphs;

    private static readonly Color Fill = ColorTranslator.FromHtml("#3b5070");

    private const int Width  = 6;
    private const int Height = 4;
    private const int Right  = Width - 1;
    private const int Bottom = Height - 1;

    private sealed class BevelControl(ISystemTerminal terminal) : Control(terminal)
    {
        public void DrawBevel(int width, int height) =>
            DrawRectangleWithBevel(0, 0, width, height, Fill);
    }

    [Fact]
    public void Corners_Use_Quadrants_When_Supported()
    {
        AnsiScreen screen = new(Draw(DrawingCapabilities.All, Width, Height));

        Assert.Equal('▗', screen[0,     0].Glyph);
        Assert.Equal('▖', screen[Right, 0].Glyph);
        Assert.Equal('▝', screen[0,     Bottom].Glyph);
        Assert.Equal('▘', screen[Right, Bottom].Glyph);

        // The part of the corner outside the bevel shows the background.
        Assert.Equal(AnsiScreen.Transparent, screen[0, 0].Background);
    }

    public void Dispose() => Control.DrawingCapabilities = DrawingCapabilities.All;

    private static string Draw(DrawingCapabilities capabilities, int width, int height)
    {
        Control.DrawingCapabilities = capabilities;

        RecordingTerminal terminal = new();
        BevelControl control       = new(terminal) { BackgroundColour = ConsolePalette.Transparent };

        control.DrawBevel(width, height);

        return terminal.Output;
    }

    [Fact]
    public void One_Cell_Wide_Bevel_Uses_The_Diagonal_Quadrant_Glyph()
    {
        AnsiScreen screen = new(Draw(DrawingCapabilities.All, width: 1, Height));

        for (int y = 0; y < Height; y++) {
            Assert.Equal('▞', screen[0, y].Glyph);
        }
    }

    [Fact]
    public void One_Cell_Wide_Bevel_Without_Seamless_Glyphs_Is_Filled_From_The_Background()
    {
        AnsiScreen screen = new(Draw(AppleTerminal, width: 1, Height));
        string fill       = AnsiScreen.Key(Fill);

        for (int y = 0; y < Height; y++) {
            Assert.Equal((fill, fill), screen.TopAndBottomAt(0, y));
            Assert.Equal(fill, screen[0, y].Background);
        }
    }

    [Theory]
    [InlineData(1,     Height)]
    [InlineData(2,     Height)]
    [InlineData(Width, 2)]
    public void Without_Quadrants_A_Bevel_Too_Small_To_Shrink_Is_Filled(int width, int height)
    {
        AnsiScreen screen = new(Draw(Conhost, width, height));
        string fill       = AnsiScreen.Key(Fill);

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                Assert.Equal(new AnsiScreen.Cell(' ', fill, fill), screen[x, y]);
            }
        }
    }

    // Regression test: without corner glyphs a selected PERFORMANCE panel's bevel had half-cell edges
    // meeting at blank corners. It is now a block of whole cells, one cell in from each edge.
    [Theory]
    [InlineData(Conhost)]
    [InlineData(DrawingCapabilities.None)]
    public void Without_Quadrants_The_Bevel_Is_A_Whole_Cell_Block_One_Cell_In(DrawingCapabilities capabilities)
    {
        AnsiScreen screen = new(Draw(capabilities, Width, Height));
        string fill       = AnsiScreen.Key(Fill);

        for (int y = 0; y <= Bottom; y++) {
            for (int x = 0; x <= Right; x++) {
                bool edge = x == 0 || x == Right || y == 0 || y == Bottom;

                Assert.Equal(' ', screen[x, y].Glyph);
                Assert.Equal(edge ? AnsiScreen.Transparent : fill, screen[x, y].Background);
            }
        }
    }

    // Regression test: none of the fonts conhost offers by default has the quadrant glyphs,
    // so a selected PERFORMANCE panel's bevel drew '?' in each corner.
    [Theory]
    [InlineData(Width)]
    [InlineData(1)]
    public void Without_Quadrant_Support_No_Quadrant_Glyph_Is_Written(int width)
    {
        string output = Draw(Conhost, width, Height);

        Assert.Equal(-1, output.IndexOfAny(QuadrantGlyphs.ToCharArray()));
    }

    // Regression test: Terminal.app draws block glyphs from the font, which leaves a strip at the
    // top of the cell uncovered, so a selected PERFORMANCE panel's sides and bottom edge were dashed.
    [Theory]
    [InlineData(AppleTerminal, Width)]
    [InlineData(AppleTerminal, 1)]
    [InlineData(DrawingCapabilities.None, Width)]
    public void Without_Seamless_Glyphs_No_Glyph_Reaching_The_Cell_Top_Is_Written(DrawingCapabilities capabilities, int width)
    {
        string output = Draw(capabilities, width, Height);

        Assert.Equal(-1, output.IndexOfAny(TopReachingGlyphs.ToCharArray()));
    }

    // The sides become whole cells, but the top and bottom keep their half-cell margins: the bottom
    // row's bevel colour comes from the cell background, with the outside drawn in reverse video.
    [Fact]
    public void Without_Seamless_Glyphs_The_Bevel_Keeps_Half_Cell_Top_And_Bottom_Edges()
    {
        AnsiScreen screen = new(Draw(AppleTerminal, Width, Height));
        string fill       = AnsiScreen.Key(Fill);

        for (int x = 0; x <= Right; x++) {
            Assert.Equal((AnsiScreen.Transparent, fill), screen.TopAndBottomAt(x, 0));
            Assert.Equal((fill, AnsiScreen.Transparent), screen.TopAndBottomAt(x, Bottom));
            Assert.Equal(fill, screen[x, Bottom].Background);

            for (int y = 1; y < Bottom; y++) {
                Assert.Equal((fill, fill), screen.TopAndBottomAt(x, y));
            }
        }
    }

    [Fact]
    public void With_Quadrants_The_Edges_Are_Half_Cells()
    {
        AnsiScreen screen = new(Draw(DrawingCapabilities.All, Width, Height));
        string fill       = AnsiScreen.Key(Fill);

        for (int x = 1; x < Right; x++) {
            Assert.Equal(new AnsiScreen.Cell('▄', fill, AnsiScreen.Transparent), screen[x, 0]);
            Assert.Equal(new AnsiScreen.Cell('▀', fill, AnsiScreen.Transparent), screen[x, Bottom]);
        }

        for (int y = 1; y < Bottom; y++) {
            Assert.Equal((AnsiScreen.Transparent, fill), screen.HalvesAt(0,     y));
            Assert.Equal((fill, AnsiScreen.Transparent), screen.HalvesAt(Right, y));

            for (int x = 1; x < Right; x++) {
                Assert.Equal((fill, fill), screen.HalvesAt(x, y));
            }
        }
    }
}
