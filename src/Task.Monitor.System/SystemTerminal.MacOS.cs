using System.Runtime.InteropServices;

namespace Task.Monitor.System;

public partial class SystemTerminal : ISystemTerminal
{
#if __APPLE__
    private const string AnsiHideCursor = "\x1b[?25l";
    private const string AnsiShowCursor = "\x1b[?25h";
    private bool cursorVisible;
    
    private bool CursorVisibleInternal
    {
        get => cursorVisible;
        set {
            cursorVisible = value;
            Console.Out.Write(cursorVisible ? AnsiShowCursor : AnsiHideCursor);
        }
    }
    
    private void EnableAnsiTerminalCodesInternal()
    {
        
    }
#endif
}