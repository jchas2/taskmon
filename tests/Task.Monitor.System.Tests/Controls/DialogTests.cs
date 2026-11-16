using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls;

namespace Task.Monitor.System.Tests.Controls;

// Every colour differs, so a line drawn in the text colour, or text in the border colour, fails.
public sealed class DialogTests
{
    private static readonly Color Background       = Color.FromArgb(1, 1, 1);
    private static readonly Color Border           = Color.FromArgb(2, 2, 2);
    private static readonly Color ButtonBackground = Color.FromArgb(3, 3, 3);
    private static readonly Color ButtonForeground = Color.FromArgb(4, 4, 4);
    private static readonly Color Focus            = Color.FromArgb(5, 5, 5);
    private static readonly Color Foreground       = Color.FromArgb(6, 6, 6);

    private readonly CellTerminal terminal = new();

    // Records the character and colours written to each cell.
    private sealed class CellTerminal : ISystemTerminal
    {
        private readonly Dictionary<(int X, int Y), (char Ch, Color Fg, Color Bg)> cells = [];

        private int x;
        private int y;

        public Color BackgroundColor { get; set; }

        public void Clear() => cells.Clear();

        public (Color Fg, Color Bg) Colours(int col, int row)
        {
            (char _, Color fg, Color bg) = cells[(col, row)];
            return (fg, bg);
        }

        public int CursorLeft { set => x = value; }
        public int CursorTop { set => y = value; }
        public bool CursorVisible { get; set; }
        public void EnableAnsiTerminalCodes() { }
        public Color ForegroundColor { get; set; }
        public bool KeyAvailable => false;
        public ConsoleKeyInfo ReadKey() => default;

        public void SetCursorPosition(int left, int top)
        {
            x = left;
            y = top;
        }

        public TextWriter StdError => TextWriter.Null;
        public TextReader StdIn => TextReader.Null;
        public TextWriter StdOut => TextWriter.Null;

        public string Text(int col, int row, int length) =>
            new([.. Enumerable.Range(col, length).Select(c => cells.TryGetValue((c, row), out var cell) ? cell.Ch : '\0')]);

        public int WindowHeight => 24;
        public int WindowWidth => 80;

        public void Write(char ch) => cells[(x++, y)] = (ch, ForegroundColor, BackgroundColor);

        public void Write(char ch, int count)
        {
            for (int i = 0; i < count; i++) {
                Write(ch);
            }
        }

        public void Write(ReadOnlySpan<char> chars)
        {
            foreach (char ch in chars) {
                Write(ch);
            }
        }

        public void Write(string message) => Write(message.AsSpan());
        public void WriteEmptyLine() { }
        public void WriteEmptyLineTo(int col) { }
        public void WriteLine(char ch) { }
        public void WriteLine(string message) { }
    }

    // Opens up the protected drawing methods.
    private sealed class TestDialog(ISystemTerminal terminal) : Dialog(terminal)
    {
        public void Bottom(int y) => DrawBottomLine(y);

        public void Button(string text, bool focused) =>
            DrawButton(0, 0, 10, text, focused, ButtonBackground, ButtonForeground, Focus);

        public void Row(int y, string text) => DrawRow(y, text);

        public void Side(int x, int y) => DrawSide(x, y);

        public void Top() => DrawTopLine();
    }

    [Fact]
    public void An_Unfocused_Button_Has_No_Focus_Colour()
    {
        CreateDialog(width: 10).Button("Cancel", focused: false);

        Assert.All(Enumerable.Range(0, 10), x => Assert.Equal((ButtonForeground, ButtonBackground), terminal.Colours(x, 0)));
    }

    // Colours are read as each part is drawn, so a theme change shows on the next draw.
    [Fact]
    public void A_Colour_Change_Shows_On_The_Next_Draw()
    {
        TestDialog dialog = CreateDialog(width: 10);
        dialog.Row(0, string.Empty);

        dialog.DialogBorderColour = Focus;
        dialog.Row(0, string.Empty);

        Assert.Equal((Focus, Background), terminal.Colours(0, 0));
    }

    [Fact]
    public void A_Dialog_Defaults_To_Black_On_Grey_With_A_Black_Border()
    {
        TestDialog dialog = new(terminal);

        Assert.Equal(ConsolePalette.Gray, dialog.DialogBackgroundColour);
        Assert.Equal(ConsolePalette.Black, dialog.DialogBorderColour);
        Assert.Equal(ConsolePalette.Black, dialog.DialogForegroundColour);
        Assert.Empty(dialog.Title);
    }

    [Fact]
    public void A_Row_Draws_Its_Sides_In_The_Border_Colour_And_Its_Text_In_The_Text_Colour()
    {
        CreateDialog(width: 10).Row(0, "Help");

        Assert.Equal("│  Help  │", terminal.Text(0, 0, 10));
        Assert.Equal((Border, Background), terminal.Colours(0, 0));
        Assert.Equal((Foreground, Background), terminal.Colours(3, 0));
        Assert.Equal((Border, Background), terminal.Colours(9, 0));
    }

    [Fact]
    public void A_Side_Takes_The_Border_Colour()
    {
        CreateDialog(width: 10).Side(4, 2);

        Assert.Equal("│", terminal.Text(4, 2, 1));
        Assert.Equal((Border, Background), terminal.Colours(4, 2));
    }

    private TestDialog CreateDialog(int width, string title = "") =>
        new(terminal) {
            DialogBackgroundColour = Background,
            DialogBorderColour     = Border,
            DialogForegroundColour = Foreground,
            Title                  = title,
            Width                  = width
        };

    [Fact]
    public void The_Bottom_Line_Takes_The_Border_Colour()
    {
        CreateDialog(width: 10).Bottom(0);

        Assert.Equal("╰────────╯", terminal.Text(0, 0, 10));
        Assert.All(Enumerable.Range(0, 10), x => Assert.Equal((Border, Background), terminal.Colours(x, 0)));
    }

    [Fact]
    public void The_Focused_Button_Marks_Only_Its_First_Letter_In_The_Focus_Colour()
    {
        CreateDialog(width: 10).Button("OK", focused: true);

        Assert.Equal("    OK    ", terminal.Text(0, 0, 10));
        Assert.Equal((Focus, ButtonBackground), terminal.Colours(4, 0));
        Assert.Equal((ButtonForeground, ButtonBackground), terminal.Colours(5, 0));
    }

    [Fact]
    public void The_Top_Line_Centres_The_Title_In_The_Text_Colour_Between_Border_Lines()
    {
        CreateDialog(width: 12, title: "Pick").Top();

        Assert.Equal("╭── Pick ──╮", terminal.Text(0, 0, 12));
        Assert.Equal((Border, Background), terminal.Colours(0, 0));
        Assert.Equal((Border, Background), terminal.Colours(2, 0));
        Assert.Equal((Foreground, Background), terminal.Colours(4, 0));
        Assert.Equal((Border, Background), terminal.Colours(11, 0));
    }

    [Fact]
    public void The_Top_Line_Cuts_A_Title_Too_Long_To_Fit()
    {
        CreateDialog(width: 8, title: "A long title").Top();

        Assert.Equal("╭ A lon╮", terminal.Text(0, 0, 8));
    }
}
