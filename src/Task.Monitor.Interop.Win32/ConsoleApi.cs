using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class ConsoleApi
{
    public const uint ENABLE_PROCESSED_OUTPUT = 0x0001;
    public const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    public const uint DISABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0008;
   
    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);
}