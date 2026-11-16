using System.Drawing;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Controls;

// A pop-up dialog: a framed box on the dialog background, with a title in its top line. The
// frame's lines take the border colour; its title and the text inside it take the foreground
// colour. Rows span the dialog's own X and Width, and colours are read as each part is drawn,
// so a theme change shows on the next draw.
public abstract class Dialog : Control
{
    protected Dialog(ISystemTerminal terminal) : base(terminal) { }

    public Color DialogBackgroundColour { get; set; } = ConsolePalette.Gray;
    public Color DialogBorderColour { get; set; } = ConsolePalette.Black;
    public Color DialogForegroundColour { get; set; } = ConsolePalette.Black;

    protected void DrawBackground() => DrawRectangle(X, Y, Width, Height, DialogBackgroundColour);

    protected void DrawBottomLine(int y)
    {
        SetFrameColours(DialogBorderColour);
        Terminal.SetCursorPosition(X, y);
        Terminal.Write('╰');
        Terminal.Write('─', InnerWidth);
        Terminal.Write('╯');
    }

    // The first letter of the focused button takes the focus colour, so it stands out from the other.
    protected void DrawButton(
        int    x,
        int    y,
        int    width,
        string text,
        bool   focused,
        Color  background,
        Color  foreground,
        Color  focus)
    {
        Terminal.BackgroundColor = background;
        Terminal.ForegroundColor = foreground;
        Terminal.SetCursorPosition(x, y);

        bool marked = !focused;

        foreach (char ch in text.CentreWithLength(width)) {
            if (!marked && !char.IsWhiteSpace(ch)) {
                Terminal.ForegroundColor = focus;
                Terminal.Write(ch);
                Terminal.ForegroundColor = foreground;
                marked = true;
                continue;
            }

            Terminal.Write(ch);
        }
    }

    // A row of the frame: the side lines with the text centred between them.
    protected void DrawRow(int y, string text)
    {
        DrawSide(X, y);
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.Write(text.CentreWithLength(InnerWidth));
        Terminal.ForegroundColor = DialogBorderColour;
        Terminal.Write('│');
    }

    // One cell of a side line, beside a row the dialog draws itself (such as its list).
    protected void DrawSide(int x, int y)
    {
        SetFrameColours(DialogBorderColour);
        Terminal.SetCursorPosition(x, y);
        Terminal.Write('│');
    }

    // The top line, with the title centred in it.
    protected void DrawTopLine()
    {
        string padded   = Title.Length > 0 ? $" {Title} " : string.Empty;
        string shown    = padded.Length > InnerWidth ? padded[..InnerWidth] : padded;
        int leftDashes  = (InnerWidth - shown.Length) / 2;
        int rightDashes = InnerWidth - shown.Length - leftDashes;

        SetFrameColours(DialogBorderColour);
        Terminal.SetCursorPosition(X, Y);
        Terminal.Write('╭');
        Terminal.Write('─', leftDashes);
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.Write(shown);
        Terminal.ForegroundColor = DialogBorderColour;
        Terminal.Write('─', rightDashes);
        Terminal.Write('╮');
    }

    // The width between the side lines.
    private int InnerWidth => Math.Max(0, Width - 2);

    private void SetFrameColours(Color lineColour)
    {
        Terminal.BackgroundColor = DialogBackgroundColour;
        Terminal.ForegroundColor = lineColour;
    }

    public string Title { get; set; } = string.Empty;
}
