using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Gui.Controls;

public class FilterControl(ISystemTerminal terminal, AppConfig appConfig) : Control(terminal)
{
    private const int CommandLength = 6;
    
    public int NeededWidth { get; private set; }
    
    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();
            OnDrawInternal();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }
    
    private void OnDrawInternal()
    {
        using TerminalColourRestorer _ = new();
        
        Terminal.SetCursorPosition(left: X, top: Y);

        int nchars = 0;
        
        nchars += KeyBindControl.Draw(
            "ENTER",
            "Done",
            nchars,
            Y,
            CommandLength,
            appConfig.DefaultTheme,
            enabled: true,
            Terminal);

        nchars += KeyBindControl.Draw(
            "ESC",
            "Clear",
            nchars,
            Y,
            CommandLength,
            appConfig.DefaultTheme,
            enabled: true,
            Terminal);

        Terminal.WriteEmptyLineTo(Width - nchars);
        Terminal.SetCursorPosition(left: nchars, top: Y);

        string spacer = "  ";
        string filterCommand = "Filter: ";

        Terminal.BackgroundColor = appConfig.DefaultTheme.Background;
        Terminal.ForegroundColor = appConfig.DefaultTheme.Foreground;
        Terminal.Write(spacer);
        nchars += spacer.Length;
        
        Terminal.BackgroundColor = appConfig.DefaultTheme.BackgroundHighlight;
        Terminal.ForegroundColor = appConfig.DefaultTheme.ForegroundHighlight;
        Terminal.Write(filterCommand);
        nchars += filterCommand.Length;
        
        NeededWidth = nchars;
    }
}
