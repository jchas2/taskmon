using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Screens;

namespace Task.Monitor.System.Controls.MessageBox;

public sealed class MessageBox : Dialog
{
    public const int PreferredHeight = 11;

    private const int MinWidth       = 40;
    private const int MaxTextLines   = 3;
    private const int MinButtonWidth = 10;
    private const int ButtonGap      = 6;

    private bool okFocused = true;
    
    public MessageBox(ISystemTerminal terminal) : base(terminal) { }

    public MessageBoxButtons Buttons { get; set; } = MessageBoxButtons.OkCancel;

    private int ButtonWidth => Math.Max(MinButtonWidth, Math.Max(OkText.Length, CancelText.Length) + 2);

    public string CancelText { get; set; } = string.Empty;

    public Color DialogButtonBackgroundColour { get; set; } = ConsolePalette.DarkGray;
    public Color DialogButtonFocusColour { get; set; } = ConsolePalette.Red;
    public Color DialogButtonForegroundColour { get; set; } = ConsolePalette.Black;

    private void DrawButton(int x, int y, string text, bool selected) =>
        base.DrawButton(
            x,
            y,
            ButtonWidth,
            text,
            selected,
            DialogButtonBackgroundColour,
            DialogButtonForegroundColour,
            DialogButtonFocusColour);

    public string HelpText { get; set; } = string.Empty;
    public string OkText { get; set; } = string.Empty;

    protected override void OnDraw()
    {
        if (Width < MinWidth || Height < PreferredHeight) {
            return;
        }
    
        DrawBackground();

        int y = Y;

        DrawTopLine();
        DrawRow(++y, string.Empty);

        string[] lines = Text.Split('\n');

        for (int n = 0; n < MaxTextLines; n++) {
            DrawRow(++y, n < lines.Length ? lines[n] : string.Empty);
        }

        for (int n = 0; n < 2; n++) {
            DrawRow(++y, string.Empty);
        }

        int buttonX = Buttons == MessageBoxButtons.Ok
            ? X + (Width / 2 - ButtonWidth / 2)
            : X + (Width / 2 - (ButtonWidth + ButtonGap + ButtonWidth) / 2);

        int buttonY = ++y;
        DrawRow(buttonY, string.Empty);

        if (Buttons == MessageBoxButtons.Ok || Buttons == MessageBoxButtons.OkCancel) {
            DrawButton(buttonX, buttonY, OkText, selected: okFocused);
        }

        if (Buttons == MessageBoxButtons.OkCancel) {
            DrawButton(buttonX + ButtonWidth + ButtonGap, buttonY, CancelText, selected: !okFocused);
        }

        DrawRow(++y, string.Empty);
        DrawRow(++y, HelpText);
        DrawBottomLine(++y);
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

            case var _ when StartsWithKey(OkText, keyInfo):
                okFocused = true;
                break;

            case var _ when StartsWithKey(CancelText, keyInfo):
                okFocused = Buttons == MessageBoxButtons.Ok;
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
    
    private static bool StartsWithKey(string label, ConsoleKeyInfo keyInfo) =>
        label.Length > 0 &&
        keyInfo.KeyChar != '\0' &&
        char.ToUpperInvariant(label[0]) == char.ToUpperInvariant(keyInfo.KeyChar);

    public string Text { get; set; } = string.Empty;
}
