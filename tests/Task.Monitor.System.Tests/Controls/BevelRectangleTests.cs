using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls;

namespace Task.Monitor.System.Tests.Controls;

// Control.QuadrantGlyphsSupported is a process-wide static; each test sets it and Dispose
// puts back the default.
public sealed class BevelRectangleTests : IDisposable
{
    private const string QuadrantGlyphs = "▖▗▘▝▞";

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

    public void Dispose() => Control.QuadrantGlyphsSupported = true;

    private static string Draw(bool quadrantsSupported, int width, int height)
    {
        Control.QuadrantGlyphsSupported = quadrantsSupported;

        RecordingTerminal terminal = new();
        BevelControl control       = new(terminal) { BackgroundColour = ConsolePalette.Transparent };

        control.DrawBevel(width, height);

        return terminal.Output;
    }

    // Regression test: none of the fonts conhost offers by default has the quadrant glyphs,
    // so a selected PERFORMANCE panel's bevel drew '?' in each corner.
    [Theory]
    [InlineData(Width)]
    [InlineData(1)]
    public void Without_Quadrant_Support_No_Quadrant_Glyph_Is_Written(int width)
    {
        string output = Draw(quadrantsSupported: false, width, Height);

        Assert.Equal(-1, output.IndexOfAny(QuadrantGlyphs.ToCharArray()));
    }

    [Theory]
    [InlineData(true,  '▗', '▖', '▝', '▘')]
    [InlineData(false, ' ', ' ', ' ', ' ')]
    public void Corners_Use_Quadrants_Only_When_Supported(
        bool supported,
        char topLeft,
        char topRight,
        char bottomLeft,
        char bottomRight)
    {
        AnsiScreen screen = new(Draw(supported, Width, Height));

        Assert.Equal(topLeft,     screen[0,     0].Glyph);
        Assert.Equal(topRight,    screen[Right, 0].Glyph);
        Assert.Equal(bottomLeft,  screen[0,     Bottom].Glyph);
        Assert.Equal(bottomRight, screen[Right, Bottom].Glyph);

        // Whatever the glyph, the part of the corner outside the bevel shows the background.
        Assert.Equal(AnsiScreen.Transparent, screen[0, 0].Background);
    }

    // The blank corners only drop the quarter cell where two edges meet; the edges stay put.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Edges_Are_The_Same_With_Or_Without_Quadrants(bool supported)
    {
        AnsiScreen screen = new(Draw(supported, Width, Height));
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

    [Theory]
    [InlineData(true,  '▞')]
    [InlineData(false, '█')]
    public void One_Cell_Wide_Bevel_Uses_A_Full_Block_Without_Quadrants(bool supported, char glyph)
    {
        AnsiScreen screen = new(Draw(supported, width: 1, Height));

        for (int y = 0; y < Height; y++) {
            Assert.Equal(glyph, screen[0, y].Glyph);
        }
    }
}
