using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls.InputBox;

namespace Task.Monitor.System.Controls.TextInputDialog;

public sealed class TextInputDialog : Dialog
{
    public const int PreferredHeight = 8;
    
    private const int MinWidth    = 30;
    private const int MinButtonWidth = 10;
    private const int ButtonGap   = 6;
    private const int FieldRow    = 2;
    private const int ButtonRow   = 4;
    private const int HelpRow     = 6;

    private readonly TextBuffer text = new();
    
    private bool okFocused = true;
    private int  viewStart;

    public TextInputDialog(ISystemTerminal terminal) : base(terminal) { }

    private int ButtonWidth => Math.Max(MinButtonWidth, Math.Max(OkText.Length, CancelText.Length) + 2);

    public string CancelText { get; set; } = string.Empty;

    public Func<char, bool>? CharacterFilter { get; set; }

    public Color DialogButtonBackgroundColour { get; set; } = ConsolePalette.DarkGray;
    public Color DialogButtonFocusColour { get; set; } = ConsolePalette.Red;
    public Color DialogButtonForegroundColour { get; set; } = ConsolePalette.Black;

    private void DrawButton(int x, int y, string label, bool selected) =>
        base.DrawButton(
            x,
            y,
            ButtonWidth,
            label,
            selected,
            DialogButtonBackgroundColour,
            DialogButtonForegroundColour,
            DialogButtonFocusColour);

    private void DrawButtons()
    {
        int buttonX = X + (Width / 2 - (ButtonWidth + ButtonGap + ButtonWidth) / 2);
        int buttonY = Y + ButtonRow;

        DrawButton(buttonX, buttonY, OkText, selected: okFocused);
        DrawButton(buttonX + ButtonWidth + ButtonGap, buttonY, CancelText, selected: !okFocused);
    }

    private void DrawField()
    {
        int caret = text.CursorBufferPosition;

        if (caret < viewStart) {
            viewStart = caret;
        }
        else if (caret - viewStart >= FieldWidth) {
            viewStart = caret - FieldWidth + 1;
        }

        string current = text.Text;
        
        string visible = viewStart < current.Length
            ? current.Substring(viewStart, Math.Min(FieldWidth, current.Length - viewStart))
            : string.Empty;

        Terminal.SetCursorPosition(FieldX, Y + FieldRow);
        Terminal.BackgroundColor = FieldBackgroundColour;
        Terminal.ForegroundColor = FieldForegroundColour;
        Terminal.Write(visible.PadRight(FieldWidth));
    }

    private void DrawFrame()
    {
        DrawBackground();
        DrawTopLine();

        for (int row = 1; row < Height - 1; row++) {
            DrawRow(Y + row, string.Empty);
        }

        DrawBottomLine(Y + Height - 1);
    }

    private void DrawHelp()
    {
        Terminal.SetCursorPosition(X + 1, Y + HelpRow);
        Terminal.BackgroundColor = DialogBackgroundColour;
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.Write(HelpText.CentreWithLength(Width - 2));
    }

    public Color FieldBackgroundColour { get; set; } = ConsolePalette.Black;
    public Color FieldForegroundColour { get; set; } = ConsolePalette.White;

    private int FieldWidth => Math.Max(1, Width - 4);

    private int FieldX => X + 2;

    public string HelpText { get; set; } = string.Empty;

    public int MaxLength { get; set; } = 64;

    public string OkText { get; set; } = string.Empty;

    protected override void OnDraw()
    {
        if (Width < MinWidth || Height < PreferredHeight) {
            return;
        }

        DrawFrame();
        DrawField();
        DrawButtons();
        DrawHelp();
        PositionCaret();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        Result = TextInputDialogResult.None;
        handled = true;

        switch (keyInfo.Key) {
            case ConsoleKey.Enter:
                Result = okFocused ? TextInputDialogResult.Ok : TextInputDialogResult.Cancel;
                return;

            case ConsoleKey.Escape:
                Result = TextInputDialogResult.Cancel;
                return;

            case ConsoleKey.Tab:
                okFocused = !okFocused;
                DrawButtons();
                PositionCaret();
                return;

            case ConsoleKey.LeftArrow:
                text.MoveLeft();
                break;

            case ConsoleKey.RightArrow:
                text.MoveRight();
                break;

            case ConsoleKey.Home:
                while (text.MoveLeft()) { }
                break;

            case ConsoleKey.End:
                while (text.MoveRight()) { }
                break;

            case ConsoleKey.Backspace:
                text.MoveBackwards();
                break;

            case ConsoleKey.Delete:
                text.Delete();
                break;

            default:
                char ch = keyInfo.KeyChar;

                if (char.IsControl(ch) ||
                    text.Length >= MaxLength ||
                    (CharacterFilter != null && !CharacterFilter(ch))) {
                    return;
                }

                text.Add(ch);
                break;
        }

        DrawField();
        PositionCaret();
    }

    private void PositionCaret() =>
        Terminal.SetCursorPosition(FieldX + text.CursorBufferPosition - viewStart, Y + FieldRow);

    public TextInputDialogResult Result { get; private set; } = TextInputDialogResult.None;

    public void SetText(string value)
    {
        text.SetText(value);
        
        Result    = TextInputDialogResult.None;
        okFocused = true;
        viewStart = 0;
    }

    public void ShowTextInputDialog()
    {
        OnResize();
        OnDraw();
    }

    public string Text => text.Text;

    public override bool Visible
    {
        get => base.Visible;
        set {
            Terminal.CursorVisible = value;
            base.Visible = value;
        }
    }
}
