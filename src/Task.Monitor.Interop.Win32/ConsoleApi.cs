using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class ConsoleApi
{
    public const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    public const uint DISABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0008;
   
    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}