using System.Drawing;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.Gui.Controls.Mascot;

public sealed class MascotFrame
{
    private const char LowerHalf = '▄';
    private const char UpperHalf = '▀';

    private readonly MascotCell[,] cells;

    private MascotFrame(MascotCell[,] cells) => this.cells = cells;

    internal static MascotFrame FromPixels(IReadOnlyList<string> rows, IReadOnlyDictionary<char, Color> palette)
    {
        int width = rows[0].Length;

        MascotCell[,] cells = new MascotCell[rows.Count / 2, width];

        for (int y = 0; y < rows.Count / 2; y++) {
            for (int x = 0; x < width; x++) {
                Color top    = Pixel(rows[y * 2][x], palette);
                Color bottom = Pixel(rows[y * 2 + 1][x], palette);

                cells[y, x] = (top.IsTransparent(), bottom.IsTransparent()) switch {
                    (false, _)    => new MascotCell(UpperHalf, top, bottom),
                    (true, false) => new MascotCell(LowerHalf, bottom, ConsolePalette.Transparent),
                    (true, true)  => new MascotCell(' ', ConsolePalette.Transparent, ConsolePalette.Transparent)
                };
            }
        }

        return new MascotFrame(cells);
    }

    public int Height => cells.GetLength(0);

    private static Color Pixel(char letter, IReadOnlyDictionary<char, Color> palette) =>
        letter == MascotAnimation.TransparentPixel 
            ? ConsolePalette.Transparent 
            : palette[letter];

    public MascotCell this[int x, int y] => cells[y, x];

    public int Width => cells.GetLength(1);
}
