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
    
        using TerminalColourRestorer _ = new();

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

        using TerminalColourRestorer _ = new();

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
                    Terminal.CursorLeft--;
                    Terminal.Write(textBuffer.Text.Substring(textBuffer.CursorBufferPosition) + " ");
                    Terminal.CursorLeft -= textBuffer.Text.Length - textBuffer.CursorBufferPosition + 1;
                }
                break;
            
            case ConsoleKey.Delete:
                if (textBuffer.Delete()) {
                    Terminal.Write(textBuffer.Text.Substring(textBuffer.CursorBufferPosition) + " ");
                    Terminal.CursorLeft -= textBuffer.Length - textBuffer.CursorBufferPosition + 1;
                }
                break;
            
            case ConsoleKey.LeftArrow:
                if (textBuffer.MoveLeft()) {
                    Terminal.CursorLeft--;
                }
                break;
            
            case ConsoleKey.RightArrow:
                if (textBuffer.MoveRight()) {
                    Terminal.CursorLeft++;
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
                    int currentCursorPosition = Terminal.CursorLeft;
                    Terminal.Write(textBuffer.Text[(textBuffer.CursorBufferPosition - 1)..]);
                    Terminal.CursorLeft = currentCursorPosition + 1;
                }
                else {
                    Terminal.Write(keyInfo.KeyChar);
                }

                break;
        }
    }

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
