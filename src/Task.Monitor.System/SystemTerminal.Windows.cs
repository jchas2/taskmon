using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System;

public partial class SystemTerminal
{
#if __WIN32__    
    private bool CursorVisibleInternal
    {
        get {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return Console.CursorVisible;
            }

            return false;
        }
        set {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Console.CursorVisible = value;
            }
        }
    }

    private void EnableAnsiTerminalCodesInternal()
    {
        // Not all terminals on Windows platforms (cmd.exe as an example) support ANSI escape codes.
        // We attempt to enable here to support older terminals.
        
        IntPtr consoleHandle = ProcessEnv.GetStdHandle(ProcessEnv.STD_OUTPUT_HANDLE);
        
        if (consoleHandle == IntPtr.Zero || consoleHandle == new IntPtr(-1)) {
            Win32ErrorHelpers.AssertOnLastError(nameof(ProcessEnv.GetStdHandle));
            return;
        }

        if (!ConsoleApi.GetConsoleMode(consoleHandle, out uint originalMode)) {
            Win32ErrorHelpers.AssertOnLastError(nameof(ConsoleApi.GetConsoleMode));
            return;
        }

        uint newMode = originalMode | ConsoleApi.ENABLE_VIRTUAL_TERMINAL_PROCESSING;

        if (!ConsoleApi.SetConsoleMode(consoleHandle, newMode)) {
            Win32ErrorHelpers.AssertOnLastError(nameof(ConsoleApi.SetConsoleMode));
        }
    }
#endif
}