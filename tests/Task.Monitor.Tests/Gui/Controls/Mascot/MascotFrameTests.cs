using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui.Controls.Mascot;

namespace Task.Monitor.Tests.Gui.Controls.Mascot;

public sealed class MascotFrameTests
{
    private static readonly Color Red  = Color.FromArgb(255, 0, 0);
    private static readonly Color Blue = Color.FromArgb(0, 0, 255);

    private static readonly Dictionary<char, Color> Palette = new() { ['r'] = Red, ['b'] = Blue };

    // Column by column: both pixels, top only, bottom only, neither.
    private static readonly MascotFrame Frame = MascotFrame.FromPixels(["rr..", "b.b."], Palette);

    [Fact]
    public void FromPixels_Draws_An_Upper_Half_Over_The_Bottom_Pixel() =>
        Assert.Equal(new MascotCell('▀', Red, Blue), Frame[0, 0]);

    // A transparent bottom pixel is left as the cell background, where the screen shows through.
    [Fact]
    public void FromPixels_Draws_An_Upper_Half_When_Only_The_Top_Pixel_Shows() =>
        Assert.Equal(new MascotCell('▀', Red, ConsolePalette.Transparent), Frame[1, 0]);

    [Fact]
    public void FromPixels_Draws_A_Lower_Half_When_Only_The_Bottom_Pixel_Shows() =>
        Assert.Equal(new MascotCell('▄', Blue, ConsolePalette.Transparent), Frame[2, 0]);

    [Fact]
    public void FromPixels_Draws_A_Space_When_Neither_Pixel_Shows() =>
        Assert.Equal(new MascotCell(' ', ConsolePalette.Transparent, ConsolePalette.Transparent), Frame[3, 0]);

    [Fact]
    public void FromPixels_Packs_Two_Pixel_Rows_Into_Each_Text_Row()
    {
        MascotFrame frame = MascotFrame.FromPixels(["rr", "bb", "..", "rb"], Palette);

        Assert.Equal(2, frame.Width);
        Assert.Equal(2, frame.Height);
    }
}
