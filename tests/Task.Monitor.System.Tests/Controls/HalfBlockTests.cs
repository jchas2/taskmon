using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls;

namespace Task.Monitor.System.Tests.Controls;

// A strip with half-cell ends, as the main screen's banners draw it. Control.DrawingCapabilities
// is a process-wide static; each test sets it and Dispose puts back the default.
public sealed class HalfBlockTests : IDisposable
{
    private const string Title = "VIEW MENU";

    private const int StripWidth = 16;
    private const int Right      = StripWidth - 1;

    private static readonly Color ScreenColour = ColorTranslator.FromHtml("#0f1610");
    private static readonly Color StripColour  = ColorTranslator.FromHtml("#1a1b26");
    private static readonly Color TextColour   = ColorTranslator.FromHtml("#9ece6a");

    private sealed class StripControl(ISystemTerminal terminal) : Control(terminal)
    {
        public void DrawStrip(Color screen, Color strip)
        {
            AnsiScreenBuffer frame = new();

            frame.MoveTo(0, 0);
            AppendHalfBlock(frame, screen, strip, fallback: screen);
            frame.SetColour(TextColour, strip);
            frame.Append(Title.PadOrTruncate(' ', StripWidth - 2));
            AppendHalfBlock(frame, strip, screen, fallback: screen);
            frame.ResetColour();
            Terminal.Write(frame.AsSpan());
        }
    }

    public void Dispose() => Control.DrawingCapabilities = DrawingCapabilities.All;

    private static AnsiScreen Draw(DrawingCapabilities capabilities, Color? screen = null, Color? strip = null)
    {
        Control.DrawingCapabilities = capabilities;

        RecordingTerminal terminal = new();
        new StripControl(terminal).DrawStrip(screen ?? ScreenColour, strip ?? StripColour);

        return new AnsiScreen(terminal.Output);
    }

    [Theory]
    [InlineData(true,  false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true,  true)]
    public void Half_Cell_Edges_Keep_A_Transparent_Colour_As_The_Cell_Background(bool screenTransparent, bool stripTransparent)
    {
        Color screenBg = screenTransparent ? ConsolePalette.Transparent : ScreenColour;
        Color stripBg  = stripTransparent ? ConsolePalette.Transparent : StripColour;

        AnsiScreen screen = Draw(DrawingCapabilities.All, screenBg, stripBg);

        string outside = AnsiScreen.Key(screenBg);
        string inside  = AnsiScreen.Key(stripBg);

        Assert.Equal((outside, inside), screen.HalvesAt(0,     0));
        Assert.Equal((inside, outside), screen.HalvesAt(Right, 0));
    }

    // conhost lacks only the quadrant glyphs, so it gets the half-cell edges too.
    [Theory]
    [InlineData(DrawingCapabilities.All)]
    [InlineData(DrawingCapabilities.SeamlessBlockGlyphs)]
    public void Half_Cell_Edges_Run_From_The_Middle_Of_The_First_Cell_To_The_Middle_Of_The_Last(DrawingCapabilities capabilities)
    {
        AnsiScreen screen = Draw(capabilities);

        string outside = AnsiScreen.Key(ScreenColour);
        string inside  = AnsiScreen.Key(StripColour);

        Assert.Equal((outside, inside), screen.HalvesAt(0,     0));
        Assert.Equal((inside, outside), screen.HalvesAt(Right, 0));
        Assert.Equal(Title[0], screen[1, 0].Glyph);
    }

    // Terminal.app leaves a seam above '▌' and '▐', so there the edge cells show the screen
    // behind the strip and the strip stops half a cell inside the borders.
    [Fact]
    public void Without_Seamless_Glyphs_The_Edge_Cells_Show_The_Screen_Behind_The_Strip()
    {
        AnsiScreen screen = Draw(DrawingCapabilities.QuadrantGlyphs);
        string outside    = AnsiScreen.Key(ScreenColour);

        Assert.Equal((outside, outside), screen.HalvesAt(0,     0));
        Assert.Equal((outside, outside), screen.HalvesAt(Right, 0));
        Assert.Equal(Title[0], screen[1, 0].Glyph);
    }
}
