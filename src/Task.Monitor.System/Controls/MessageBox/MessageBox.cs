using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Screens;

namespace Task.Monitor.System.Controls.MessageBox;

public sealed class MessageBox : Control
{
    private const int MinWidth = 40;
    private const int MinHeight = 11;
    private const int MaxTextLines = 3;
    private const int ButtonWidth = 10;
    private const int ButtonHeight = 1;
    private const int ButtonGap = 6;

    private bool okFocused = true;
    
    public MessageBox(ISystemTerminal terminal) : base(terminal) { }

    public MessageBoxButtons Buttons { get; set; } = MessageBoxButtons.OkCancel;

    public Color DialogBackgroundColour { get; set; } = ConsolePalette.Gray;
    public Color DialogBorderColour { get; set; } = ConsolePalette.Black;
    public Color DialogButtonBackgroundColour { get; set; } = ConsolePalette.DarkGray;
    public Color DialogButtonForegroundColour { get; set; } = ConsolePalette.Black;
    public Color DialogForegroundColour { get; set; } = ConsolePalette.Black;
    
    private void DrawButton(
        int x,
        int y,
        int width,
        int height,
        string text,
        bool selected)
    {
        using TerminalColourRestorer _ = new();
        
        DrawRectangle(
            x,
            y,
            width,
            height,
            DialogBackgroundColour);
        
        string centredText = text.CentreWithLength(width);
        Terminal.BackgroundColor = DialogButtonBackgroundColour;
        Terminal.ForegroundColor = DialogButtonForegroundColour;
        
        Terminal.SetCursorPosition(x, y);

        bool isHighlightChar = true;
        
        foreach (char ch in centredText) {
            
            if (char.IsWhiteSpace(ch)) {
                Terminal.Write(ch);                
            }
            else {
                
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
    }
    
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
            DialogBackgroundColour);
        
        int y = Y;

        int dialogWidth = Width - 2;
        string title = Title.Length > 0 ? $" {Title} " : string.Empty;
        int titleLen = Math.Min(title.Length, dialogWidth);
        int leftDashes = (dialogWidth - titleLen) / 2;
        int rightDashes = dialogWidth - titleLen - leftDashes;

        Terminal.BackgroundColor = DialogBackgroundColour;
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.SetCursorPosition(X, y);
        Terminal.Write('\u256D');
        Terminal.Write('\u2500', leftDashes);
        Terminal.Write(titleLen < title.Length ? title[..titleLen] : title);
        Terminal.Write('\u2500', rightDashes);
        Terminal.Write('\u256E');
        
        string spacer = new(' ', dialogWidth);
        Terminal.SetCursorPosition(X, ++y);
        Terminal.Write($"\u2502{spacer}\u2502");
        
        string[] lines = Text.Split('\n');
        
        for (int n = 0; n < MaxTextLines; n++) { 
            Terminal.SetCursorPosition(X, ++y);
         
            if (n < lines.Length) { 
                Terminal.Write($"\u2502{lines[n].CentreWithLength(dialogWidth)}\u2502");
                continue;
            }
         
            Terminal.Write($"\u2502{spacer}\u2502");
        }

        for (int n = 0; n < 2; n++) {
            Terminal.SetCursorPosition(X, ++y);
            Terminal.Write($"\u2502{spacer}\u2502");
        }

        int buttonX = Buttons == MessageBoxButtons.Ok
            ? X + (Width / 2 - ButtonWidth / 2)
            : X + (Width / 2 - (ButtonWidth + ButtonGap + ButtonWidth) / 2);

        int buttonY = ++y;
        Terminal.SetCursorPosition(X, buttonY);
        Terminal.Write($"\u2502{spacer}\u2502");
         
        if (Buttons == MessageBoxButtons.Ok || Buttons == MessageBoxButtons.OkCancel) {
            DrawButton(
                buttonX,
                buttonY,
                ButtonWidth,
                ButtonHeight,
                "OK", 
                selected: okFocused);
        }
         
        if (Buttons == MessageBoxButtons.OkCancel) {
            DrawButton(
                buttonX + ButtonWidth + ButtonGap,
                buttonY, 
                ButtonWidth,
                ButtonHeight,
                "Cancel",
                selected: !okFocused);
        }

        Terminal.BackgroundColor = DialogBackgroundColour;
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.SetCursorPosition(X, ++y);
        Terminal.Write($"\u2502{spacer}\u2502");

        string help = "Use \u2190 \u2192 and \u21B5 to select";
        Terminal.SetCursorPosition(X, ++y);
        Terminal.Write($"\u2502{help.CentreWithLength(dialogWidth)}\u2502");
        
        Terminal.SetCursorPosition(X, ++y);
        Terminal.Write('\u2570');
        Terminal.Write('\u2500', dialogWidth);
        Terminal.Write('\u256F');
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        Result = MessageBoxResult.None;
        handled = true;
        
        switch (keyInfo.Key) {
            case ConsoleKey.LeftArrow:
            case ConsoleKey.O:
            case ConsoleKey.Y:
                okFocused = true;
                break;
            
            case ConsoleKey.RightArrow:
            case ConsoleKey.C:
            case ConsoleKey.N:
                okFocused = Buttons == MessageBoxButtons.Ok;
                break;
            
            case ConsoleKey.Enter:
                Result = okFocused ? MessageBoxResult.Ok : MessageBoxResult.Cancel;
                break;
            
            case ConsoleKey.Escape:
                Result = MessageBoxResult.Cancel;
                break;
        }

        OnDraw();
    }

    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public void ShowMessageBox()
    {
        OnResize();
        OnDraw();
    }
    
    public string Text { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
