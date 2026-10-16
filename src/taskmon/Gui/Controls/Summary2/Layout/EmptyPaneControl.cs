using Task.Monitor.System;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public sealed class EmptyPaneControl : Control
{
    private const string Label = "empty";

    public EmptyPaneControl(ISystemTerminal terminal) : base(terminal) { }

    protected override void OnDraw()
    {
        if (Width < 2 || Height < 2) {
            return;
        }

        int innerWidth = Width - 2;
        int labelY     = Y + Height / 2;
        
        string blank = new(' ', innerWidth);

        Terminal.BackgroundColor = BackgroundColour;

        for (int y = Y; y < Y + Height; y++) {
            Terminal.SetCursorPosition(X, y);
            Terminal.ForegroundColor = DisplayBorderColour;

            if (y == Y || y == Y + Height - 1) {
                Terminal.Write(y == Y ? '╭' : '╰');
                Terminal.Write('─', innerWidth);
                Terminal.Write(y == Y ? '╮' : '╯');
                continue;
            }

            Terminal.Write('│');

            if (y == labelY && innerWidth >= Label.Length) {
                int left = (innerWidth - Label.Length) / 2;
                Terminal.ForegroundColor = ForegroundColour;
                Terminal.Write(blank[..left]);
                Terminal.Write(Label);
                Terminal.Write(blank[..(innerWidth - left - Label.Length)]);
            }
            else {
                Terminal.Write(blank);
            }

            Terminal.ForegroundColor = DisplayBorderColour;
            Terminal.Write('│');
        }
    }
}
