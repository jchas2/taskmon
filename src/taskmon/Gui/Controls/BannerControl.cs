using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Gui.Controls;

public sealed class BannerControl(ISystemTerminal terminal, AppConfig appConfig) : Control(terminal)
{
    private readonly AnsiScreenBuffer frame = new();

    // Older terminals can't draw half-cells. 
    public bool HalfCellEdges { get; init; }

    protected override void OnDraw() => OnDrawInternal();

    private void OnDrawInternal()
    {
        Color background = appConfig.Theme.MenubarBackground;
        Color foreground = appConfig.Theme.MenubarForeground;

        frame.Clear();
        frame.MoveTo(X, Y);

        if (HalfCellEdges && Width >= 2) {
            AppendHalfBlock(frame, BackgroundColour, background, fallback: BackgroundColour);
            frame.SetColour(foreground, background);
            frame.Append(Text.PadOrTruncate(' ', Width - 2));
            AppendHalfBlock(frame, background, BackgroundColour, fallback: BackgroundColour);
        }
        else {
            frame.SetColour(foreground, background);
            frame.Append(Text.PadOrTruncate(' ', Width));
        }

        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }
    
    public string Text { get; set; } = string.Empty;
}
