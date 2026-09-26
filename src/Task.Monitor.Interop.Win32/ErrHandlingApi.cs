using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class ErrHandlingApi
{
    public const uint SEM_FAILCRITICALERRORS = 0x0001;

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern unsafe bool SetThreadErrorMode(uint dwNewMode, uint* lpOldMode);
}
