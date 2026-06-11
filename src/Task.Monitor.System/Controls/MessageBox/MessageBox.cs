using Task.Monitor.Cli.Utils;

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
    
    // TODO: Theme.
    private ConsoleColor dialogColour = ConsoleColor.Gray;
    private ConsoleColor dialogShadowColour = ConsoleColor.DarkGray;
    private ConsoleColor dialogTitleColour = ConsoleColor.White;
    
    public MessageBox(ISystemTerminal terminal) : base(terminal) { }

    public MessageBoxButtons Buttons { get; set; } = MessageBoxButtons.OkCancel;

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
            dialogTitleColour);
        
        string centredText = text.CentreWithLength(width);
        Terminal.BackgroundColor = dialogTitleColour;
        Terminal.ForegroundColor = ConsoleColor.Black;
        
        Terminal.SetCursorPosition(x, y);

        bool isHighlightChar = true;
        
        foreach (char ch in centredText) {
            
            if (char.IsWhiteSpace(ch)) {
                Terminal.Write(ch);                
            }
            else {
                
                if (isHighlightChar && selected) {
                    Terminal.ForegroundColor = ConsoleColor.Red;
                    Terminal.Write(ch);
                    Terminal.ForegroundColor = ConsoleColor.Black;
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
            X + 1,
            Y + 1,
            Width,
            Height,
            dialogShadowColour);
        
        DrawRectangle(
            X,
            Y,
            Width,
            Height,
            dialogColour);
        
        int y = Y;

         string centredTitle = Title.CentreWithLength(Width);
         Terminal.BackgroundColor = dialogTitleColour;
         Terminal.ForegroundColor = ConsoleColor.Black;
         Terminal.SetCursorPosition(X, y);
         Terminal.Write(centredTitle);
        
         string spacer = new(' ', Width);
         Terminal.BackgroundColor = dialogColour;
         Terminal.ForegroundColor = ConsoleColor.Black;
         Terminal.SetCursorPosition(X, ++y);
         Terminal.Write(spacer);
        
         string[] lines = Text.Split('\n');
        
         for (int n = 0; n < MaxTextLines; n++) {
             Terminal.SetCursorPosition(X, ++y);
             
             if (n < lines.Length) {
                 Terminal.Write(lines[n].CentreWithLength(Width));
                 continue;
             }
             
             Terminal.Write(spacer);
         }

         for (int n = 0; n < 2; n++) {
             Terminal.SetCursorPosition(X, ++y);
             Terminal.Write(spacer);
         }

         int buttonX = Buttons == MessageBoxButtons.Ok
            ? X + (Width / 2 - ButtonWidth / 2)
            : X + (Width / 2 - (ButtonWidth + ButtonGap + ButtonWidth) / 2);

         int buttonY = ++y;
         
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

         Terminal.SetCursorPosition(X, ++y);
         Terminal.Write(spacer);

         string help = "Use \u2190 \u2192 and \u21B5 to select";
         Terminal.SetCursorPosition(X, ++y);
         Terminal.Write(help.CentreWithLength(Width));
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
