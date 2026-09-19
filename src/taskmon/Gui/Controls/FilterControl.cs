using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Gui.Controls;

public class FilterControl(ISystemTerminal terminal, AppConfig appConfig) : Control(terminal)
{
    private const int CommandLength = 6;
    
    public int NeededWidth { get; private set; }
    
    protected override void OnDraw() => OnDrawInternal();
    
    private void OnDrawInternal()
    {
        Terminal.SetCursorPosition(left: X, top: Y);

        int nchars = 0;
        
        nchars += KeyBindControl.Draw(
            "ENTER",
            "Done",
            nchars,
            Y,
            CommandLength,
            appConfig.Theme,
            enabled: true,
            Terminal);

        nchars += KeyBindControl.Draw(
            "ESC",
            "Clear",
            nchars,
            Y,
            CommandLength,
            appConfig.Theme,
            enabled: true,
            Terminal);

        Terminal.WriteEmptyLineTo(Width - nchars);
        Terminal.SetCursorPosition(left: nchars, top: Y);

        string spacer = "  ";
        string filterCommand = "Filter: ";

        Terminal.BackgroundColor = appConfig.Theme.Background;
        Terminal.ForegroundColor = appConfig.Theme.Foreground;
        Terminal.Write(spacer);
        nchars += spacer.Length;
        
        Terminal.BackgroundColor = appConfig.Theme.BackgroundHighlight;
        Terminal.ForegroundColor = appConfig.Theme.ForegroundHighlight;
        Terminal.Write(filterCommand);
        nchars += filterCommand.Length;
        
        NeededWidth = nchars;
    }
}
