using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Tests.Controls;

namespace Task.Monitor.Tests.Gui.Controls;

// Control.DrawingCapabilities is a process-wide static; each test sets it and Dispose
// puts back the default.
public sealed class BannerControlTests : IDisposable
{
    private const string Title = "VIEW MENU";

    private const int BannerWidth = 16;
    private const int Right       = BannerWidth - 1;

    private static readonly Color ScreenColour = ColorTranslator.FromHtml("#0f1610");
    private static readonly Color StripColour  = ColorTranslator.FromHtml("#1a1b26");
    private static readonly Color TextColour   = ColorTranslator.FromHtml("#9ece6a");

    [Fact]
    public void A_Long_Title_Stops_Short_Of_The_Right_Edge()
    {
        string title      = new('T', BannerWidth * 2);
        AnsiScreen screen = Draw(DrawingCapabilities.All, halfCellEdges: true, title);

        Assert.Equal('T', screen[Right - 1, 0].Glyph);
        Assert.Equal((AnsiScreen.Key(StripColour), AnsiScreen.Key(ScreenColour)), screen.HalvesAt(Right, 0));
    }

    public void Dispose() => Control.DrawingCapabilities = DrawingCapabilities.All;

    private static AnsiScreen Draw(
        DrawingCapabilities capabilities,
        bool                halfCellEdges,
        string              title    = Title,
        Color?              screenBg = null,
        Color?              stripBg  = null)
    {
        Control.DrawingCapabilities = capabilities;

        RecordingTerminal terminal = new();
        AppConfig appConfig        = new RunContextHelper().GetRunContext().AppConfig;

        appConfig.Theme.MenubarBackground = stripBg ?? StripColour;
        appConfig.Theme.MenubarForeground = TextColour;

        BannerControl banner = new(terminal, appConfig) {
            X                = 0,
            Y                = 0,
            Width            = BannerWidth,
            Height           = 1,
            Visible          = true,
            BackgroundColour = screenBg ?? ScreenColour,
            HalfCellEdges    = halfCellEdges,
            Text             = title
        };

        banner.Draw();

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

        AnsiScreen screen = Draw(DrawingCapabilities.All, halfCellEdges: true, screenBg: screenBg, stripBg: stripBg);

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
        AnsiScreen screen = Draw(capabilities, halfCellEdges: true);

        string outside = AnsiScreen.Key(ScreenColour);
        string inside  = AnsiScreen.Key(StripColour);

        Assert.Equal((outside, inside), screen.HalvesAt(0,     0));
        Assert.Equal((inside, outside), screen.HalvesAt(Right, 0));
        Assert.Equal(Title[0], screen[1, 0].Glyph);
    }

    [Fact]
    public void Without_Half_Cell_Edges_The_Title_Fills_The_Banner_From_Its_First_Column()
    {
        AnsiScreen screen = Draw(DrawingCapabilities.All, halfCellEdges: false);
        string inside     = AnsiScreen.Key(StripColour);

        Assert.Equal(Title[0], screen[0, 0].Glyph);
        Assert.Equal((inside, inside), screen.HalvesAt(Right, 0));
    }

    // Terminal.app leaves a seam above '▌' and '▐', so there the edge cells show the screen
    // behind the banner and the strip stops half a cell inside the borders.
    [Fact]
    public void Without_Seamless_Glyphs_The_Edge_Cells_Show_The_Screen_Behind_The_Banner()
    {
        AnsiScreen screen = Draw(DrawingCapabilities.QuadrantGlyphs, halfCellEdges: true);
        string outside    = AnsiScreen.Key(ScreenColour);

        Assert.Equal((outside, outside), screen.HalvesAt(0,     0));
        Assert.Equal((outside, outside), screen.HalvesAt(Right, 0));
        Assert.Equal(Title[0], screen[1, 0].Glyph);
    }
}
