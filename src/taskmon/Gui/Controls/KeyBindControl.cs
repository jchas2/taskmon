using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;

namespace Task.Monitor.Gui.Controls;

public static class KeyBindControl
{
    public static int Draw(
        string keyBinding,
        string text,
        int x,
        int y,
        int width,
        Theme theme,
        bool enabled,
        ISystemTerminal terminal)
    {
        terminal.SetCursorPosition(x, y);
        terminal.BackgroundColor = theme.Background;
        terminal.ForegroundColor = enabled ? theme.Foreground : ConsoleColor.DarkGray;
        terminal.Write(keyBinding + " ");
        int nchars = keyBinding.Length + 1;
        
        terminal.BackgroundColor = theme.CommandBackground;
        terminal.ForegroundColor = enabled ? theme.CommandForeground : ConsoleColor.DarkGray;
        terminal.Write(text.CentreWithLength(width).ToBold());
        terminal.BackgroundColor = theme.Background;
        nchars += width;

        return nchars;
    }
}
