using System.Drawing;
using System.Text.RegularExpressions;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Tests.Controls;

// Replays the ANSI stream a control writes (e.g. RecordingTerminal.Output) and records the
// glyph and colours each cell ends up with. Understands only the sequences AnsiScreenBuffer
// emits for truecolour: cursor moves, 24-bit colours, default colours, reverse video and reset.
public sealed class AnsiScreen
{
    // SGR 49: the terminal's own background, which shows through.
    public const string Transparent = "transparent";

    // SGR 39: the terminal's default text colour. Not transparent.
    public const string DefaultForeground = "default";

    public readonly record struct Cell(char Glyph, string Foreground, string Background);

    private static readonly Regex Token = new(@"\u001b\[([0-9;]*)([A-Za-z])|(.)", RegexOptions.Singleline);

    private readonly Dictionary<(int X, int Y), Cell> cells = new();

    public AnsiScreen(string output)
    {
        string fg      = DefaultForeground;
        string bg      = Transparent;
        bool   reverse = false;
        int    row     = 0;
        int    col     = 0;

        foreach (Match token in Token.Matches(output)) {
            if (token.Groups[3].Success) {
                cells[(col++, row)] = reverse
                    ? new Cell(token.Groups[3].Value[0], bg, fg)
                    : new Cell(token.Groups[3].Value[0], fg, bg);
                continue;
            }

            string args = token.Groups[1].Value;

            switch (token.Groups[2].Value) {
                case "H":
                    string[] pos = args.Split(';');
                    row = int.Parse(pos[0]) - 1;
                    col = int.Parse(pos[1]) - 1;
                    break;

                case "m" when args is "0":
                    fg      = DefaultForeground;
                    bg      = Transparent;
                    reverse = false;
                    break;

                case "m" when args is "7":
                    reverse = true;
                    break;

                case "m" when args is "27":
                    reverse = false;
                    break;

                case "m" when args is "39":
                    fg = DefaultForeground;
                    break;

                case "m" when args is "49":
                    bg = Transparent;
                    break;

                case "m" when args.StartsWith("38;2;"):
                    fg = args["38;2;".Length..];
                    break;

                case "m" when args.StartsWith("48;2;"):
                    bg = args["48;2;".Length..];
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected sequence ESC[{args}{token.Groups[2].Value}");
            }
        }
    }

    // The colours the left and right halves of a cell show, for glyphs split down the middle.
    public (string Left, string Right) HalvesAt(int x, int y)
    {
        Cell cell = this[x, y];

        return cell.Glyph switch {
            '▌' => (cell.Foreground, cell.Background),
            '▐' => (cell.Background, cell.Foreground),
            ' ' => (cell.Background, cell.Background),
            '█' => (cell.Foreground, cell.Foreground),
            _   => throw new InvalidOperationException($"Cell ({x}, {y}) holds '{cell.Glyph}', not a half block")
        };
    }

    // How a colour appears in cells: Transparent, or "r;g;b".
    public static string Key(Color colour) =>
        colour.IsTransparent() ? Transparent : $"{colour.R};{colour.G};{colour.B}";

    public Cell this[int x, int y] => cells[(x, y)];

    // The colours the top and bottom halves of a cell show, for glyphs split across the middle.
    public (string Top, string Bottom) TopAndBottomAt(int x, int y)
    {
        Cell cell = this[x, y];

        return cell.Glyph switch {
            '▄' => (cell.Background, cell.Foreground),
            '▀' => (cell.Foreground, cell.Background),
            ' ' => (cell.Background, cell.Background),
            '█' => (cell.Foreground, cell.Foreground),
            _   => throw new InvalidOperationException($"Cell ({x}, {y}) holds '{cell.Glyph}', not a half block")
        };
    }
}
