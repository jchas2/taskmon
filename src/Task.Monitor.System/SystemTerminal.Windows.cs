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
        // Not all terminals on Windows platforms (conhost.exe as an example) support VT/ANSI escape codes.
        // We attempt to enable here to support older terminals.
        IntPtr consoleHandle = ProcessEnv.GetStdHandle(ProcessEnv.STD_OUTPUT_HANDLE);
        
        if (consoleHandle == IntPtr.Zero || consoleHandle == new IntPtr(-1)) {
            PInvokeErrorHelpers.AssertOnLastError(nameof(ProcessEnv.GetStdHandle));
            Trace.WriteLine($"EnableAnsiTerminalCodesInternal GetStdHandle: {PInvokeErrorHelpers.GetFormattedErrorMesage()}");
            return;
        }

        if (!ConsoleApi.GetConsoleMode(consoleHandle, out uint originalMode)) {
            PInvokeErrorHelpers.AssertOnLastError(nameof(ConsoleApi.GetConsoleMode));
            Trace.WriteLine($"EnableAnsiTerminalCodesInternal GetConsoleMode: {PInvokeErrorHelpers.GetFormattedErrorMesage()}");
            return;
        }

        uint newMode = originalMode | ConsoleApi.ENABLE_VIRTUAL_TERMINAL_PROCESSING;

        if (!ConsoleApi.SetConsoleMode(consoleHandle, newMode)) {
            PInvokeErrorHelpers.AssertOnLastError(nameof(ConsoleApi.SetConsoleMode));
            Trace.WriteLine($"EnableAnsiTerminalCodesInternal SetConsoleMode: {PInvokeErrorHelpers.GetFormattedErrorMesage()}");
        }
    }
#endif
}