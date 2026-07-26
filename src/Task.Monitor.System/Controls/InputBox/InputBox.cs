using System.Drawing;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Controls.InputBox;

public sealed class InputBox(ISystemTerminal terminal) : Control(terminal)
{
    private const int MinWidth = 10;
    private const int MinHeight = 1;

    private readonly TextBuffer textBuffer = new();
    private readonly Color boxColour = ConsolePalette.Gray;

    protected override void OnDraw()
    {
        if (Width < MinWidth || Height < MinHeight) {
            return;
        }
    
        using TerminalRestorer _ = new();

        DrawRectangle(
            X,
            Y,
            Width,
            Height,
            boxColour);
         
        Terminal.SetCursorPosition(X, Y);

        if (!string.IsNullOrEmpty(Text)) {
            Terminal.BackgroundColor = boxColour;
            Terminal.ForegroundColor = ConsolePalette.Black;
            Terminal.Write(Text);
        }
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        Result = InputBoxResult.None;
        handled = true;

        using TerminalRestorer _ = new();

        Terminal.BackgroundColor = boxColour;
        Terminal.ForegroundColor = ConsolePalette.Black;
        
        switch (keyInfo.Key) {
            case ConsoleKey.Enter:
                Result = InputBoxResult.Enter;
                break;
            
            case ConsoleKey.Escape:
                textBuffer.Clear();
                Result = InputBoxResult.Cancel;
                break;
            
            case ConsoleKey.Backspace:
                if (textBuffer.MoveBackwards()) {
                    // Redraw the tail from the caret, plus a space to clear the vacated cell.
                    PositionCaret();
                    Terminal.Write(textBuffer.Text[textBuffer.CursorBufferPosition..] + " ");
                    PositionCaret();
                }
                break;

            case ConsoleKey.Delete:
                if (textBuffer.Delete()) {
                    PositionCaret();
                    Terminal.Write(textBuffer.Text[textBuffer.CursorBufferPosition..] + " ");
                    PositionCaret();
                }
                break;

            case ConsoleKey.LeftArrow:
                if (textBuffer.MoveLeft()) {
                    PositionCaret();
                }
                break;

            case ConsoleKey.RightArrow:
                if (textBuffer.MoveRight()) {
                    PositionCaret();
                }
                break;

            case ConsoleKey.Insert:
                textBuffer.InsertMode = !textBuffer.InsertMode;
                break;

            default:
                if (!textBuffer.Add(keyInfo.KeyChar)) {
                    break;
                }

                if (textBuffer.InsertMode) {
                    // Redraw from the inserted character to the end of the text.
                    Terminal.CursorLeft = X + textBuffer.CursorBufferPosition - 1;
                    Terminal.Write(textBuffer.Text[(textBuffer.CursorBufferPosition - 1)..]);
                }
                else {
                    Terminal.Write(keyInfo.KeyChar);
                }

                PositionCaret();
                break;
        }
    }

    // The screen caret is always at column X + the buffer caret index on the box's row.
    private void PositionCaret() => Terminal.CursorLeft = X + textBuffer.CursorBufferPosition;

    public InputBoxResult Result { get; private set; } = InputBoxResult.Enter;

    public void ShowInputBox()
    {
        OnResize();
        OnDraw();
    }

    public string Text => textBuffer.Text;

    public override bool Visible
    {
        get => base.Visible;
        set {
            Terminal.CursorVisible = value;
            base.Visible = value;
        }
    }

    public string Title { get; set; } = string.Empty;
}
