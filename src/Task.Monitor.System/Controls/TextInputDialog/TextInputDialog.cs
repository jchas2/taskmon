using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls.InputBox;

namespace Task.Monitor.System.Controls.TextInputDialog;

// A MessageBox-style modal (rounded border, title bar, OK/Cancel buttons, help hint) around a
// single-line text field - DriveInputBox's chrome, with a text field in place of its list. Same
// modal contract as DriveInputBox/PickerBox: not added to a parent's Controls; a caller shows it
// with Visible = true plus Control.RedrawEnabled = false and forwards KeyPressed() to it while it
// is visible, reading Result once it is no longer None.
//
// Left/Right move the caret within the text, so - unlike DriveInputBox, whose list only needs
// Up/Down - Tab/Shift+Tab are what move between the OK and Cancel buttons.
public sealed class TextInputDialog : Control
{
    // Top border, spacer, field, spacer, button row, spacer, help row, bottom border.
    public const int PreferredHeight = 8;

    private const int MinWidth = 30;
    private const int ButtonWidth = 10;
    private const int ButtonGap = 6;
    private const int FieldRow = 2;
    private const int ButtonRow = 4;
    private const int HelpRow = 6;

    private readonly TextBuffer text = new();
    private bool okFocused = true;
    private int viewStart;

    public TextInputDialog(ISystemTerminal terminal) : base(terminal) { }

    public Color DialogBackgroundColour { get; set; } = ConsolePalette.Gray;
    public Color DialogButtonBackgroundColour { get; set; } = ConsolePalette.DarkGray;
    public Color DialogButtonForegroundColour { get; set; } = ConsolePalette.Black;
    public Color DialogForegroundColour { get; set; } = ConsolePalette.Black;
    public Color FieldBackgroundColour { get; set; } = ConsolePalette.Black;
    public Color FieldForegroundColour { get; set; } = ConsolePalette.White;

    public string Title { get; set; } = string.Empty;

    public int MaxLength { get; set; } = 64;

    // Null accepts any printable character. A caller whose text ends up somewhere with its own
    // rules (e.g. a config section name) narrows it here, so an unusable value can't be typed.
    public Func<char, bool>? CharacterFilter { get; set; }

    public TextInputDialogResult Result { get; private set; } = TextInputDialogResult.None;

    public string Text => text.Text;

    public override bool Visible
    {
        get => base.Visible;
        set {
            Terminal.CursorVisible = value;
            base.Visible = value;
        }
    }

    // Resets the text, the result and the button focus - call before each ShowTextInputDialog.
    public void SetText(string value)
    {
        text.SetText(value);
        Result = TextInputDialogResult.None;
        okFocused = true;
        viewStart = 0;
    }

    public void ShowTextInputDialog()
    {
        OnResize();
        OnDraw();
    }

    private int FieldX => X + 2;
    private int FieldWidth => Math.Max(1, Width - 4);

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

    private void DrawFrame()
    {
        DrawRectangle(X, Y, Width, Height, DialogBackgroundColour);

        int dialogWidth = Width - 2;
        string title = Title.Length > 0 ? $" {Title} " : string.Empty;
        int titleLen = Math.Min(title.Length, dialogWidth);
        int leftDashes = (dialogWidth - titleLen) / 2;
        int rightDashes = dialogWidth - titleLen - leftDashes;

        Terminal.BackgroundColor = DialogBackgroundColour;
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.SetCursorPosition(X, Y);
        Terminal.Write('╭');
        Terminal.Write('─', leftDashes);
        Terminal.Write(titleLen < title.Length ? title[..titleLen] : title);
        Terminal.Write('─', rightDashes);
        Terminal.Write('╮');

        string spacer = new(' ', dialogWidth);

        for (int row = 1; row < Height - 1; row++) {
            Terminal.SetCursorPosition(X, Y + row);
            Terminal.Write($"│{spacer}│");
        }

        Terminal.SetCursorPosition(X, Y + Height - 1);
        Terminal.Write('╰');
        Terminal.Write('─', dialogWidth);
        Terminal.Write('╯');
    }

    // The field scrolls horizontally rather than wrapping or overflowing the border - it shows
    // the window of the text that keeps the caret visible.
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

    private void DrawButtons()
    {
        int buttonX = X + (Width / 2 - (ButtonWidth + ButtonGap + ButtonWidth) / 2);
        int buttonY = Y + ButtonRow;

        DrawButton(buttonX, buttonY, "OK", selected: okFocused);
        DrawButton(buttonX + ButtonWidth + ButtonGap, buttonY, "Cancel", selected: !okFocused);
    }

    private void DrawButton(int x, int y, string label, bool selected)
    {
        string centredText = label.CentreWithLength(ButtonWidth);
        Terminal.BackgroundColor = DialogButtonBackgroundColour;
        Terminal.ForegroundColor = DialogButtonForegroundColour;
        Terminal.SetCursorPosition(x, y);

        bool isHighlightChar = true;

        foreach (char ch in centredText) {
            if (char.IsWhiteSpace(ch)) {
                Terminal.Write(ch);
                continue;
            }

            if (isHighlightChar && selected) {
                Terminal.ForegroundColor = ConsolePalette.Red;
                Terminal.Write(ch);
                Terminal.ForegroundColor = DialogButtonForegroundColour;
                isHighlightChar = false;
                continue;
            }

            Terminal.Write(ch);
        }
    }

    private void DrawHelp()
    {
        const string help = "Tab select button   ↵ confirm   Esc cancel";

        Terminal.SetCursorPosition(X + 1, Y + HelpRow);
        Terminal.BackgroundColor = DialogBackgroundColour;
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.Write(help.CentreWithLength(Width - 2));
    }

    private void PositionCaret() =>
        Terminal.SetCursorPosition(FieldX + text.CursorBufferPosition - viewStart, Y + FieldRow);

    // Redraws only what the key changed - the field row, or the button row - never the whole
    // frame, matching DriveInputBox/PickerBox (a full repaint per keystroke flickers).
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
}
