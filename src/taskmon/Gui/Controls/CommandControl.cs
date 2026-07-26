using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui.Commands;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Gui.Controls;

public sealed class CommandControl(ISystemTerminal terminal, AppConfig appConfig) : Control(terminal)
{
    private const int CommandLength = 8;

    private readonly Dictionary<ConsoleKey, AbstractCommand> commandMap = new();

    public CommandControl AddCommand(ConsoleKey key, Func<AbstractCommand> commandFactory)
    {
        commandMap.Add(key, commandFactory.Invoke());
        return this;
    }
    
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
        using TerminalRestorer _ = new();
        
        Terminal.SetCursorPosition(left: X, top: Y);
        int nchars = 0;
        
        foreach (ConsoleKey key in commandMap.Keys) {
            AbstractCommand cmd = commandMap[key];

            string keyStr = key.ToString();
            
            string commandText = cmd.Text.Length > CommandLength
                ? cmd.Text.Substring(0, CommandLength - 1)
                : cmd.Text.PadRight(CommandLength);

            if (nchars + keyStr.Length + CommandLength + 1 > Width) {
                break;
            }
            
            nchars += KeyBindControl.Draw(
                key.ToString(),
                commandText,
                nchars,
                Y,
                CommandLength,
                appConfig.DefaultTheme,
                cmd.IsEnabled,
                Terminal);
            
            Terminal.Write(' ');
            nchars++;
        }
        
        Terminal.WriteEmptyLineTo(Width - nchars);
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (!commandMap.TryGetValue(keyInfo.Key, out var cmd)) {
            return;
        }

        if (cmd.IsEnabled) {
            cmd.Execute();
        }
        
        handled = true;            
    }
}
