using Task.Monitor.System;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

// Placeholder for a PaneControlType.Empty pane - a freshly split pane in LayoutDesignerScreen
// before it has been assigned a real control, or (defensively) a saved layout with a slot nobody
// ever finished configuring, should one ever reach a live SummaryControl2 dashboard. Draws a
// border in BorderColour, like a Chart or ListView, so the designer's selection highlight (a
// BorderColour swap) shows on an empty pane the same way it does on a populated one.
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
        string blank = new(' ', innerWidth);
        int labelY = Y + Height / 2;

        Terminal.BackgroundColor = BackgroundColour;

        for (int y = Y; y < Y + Height; y++) {
            Terminal.SetCursorPosition(X, y);
            Terminal.ForegroundColor = BorderColour;

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

            Terminal.ForegroundColor = BorderColour;
            Terminal.Write('│');
        }
    }
}
