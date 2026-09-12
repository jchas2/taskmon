using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class ErrHandlingApi
{
    // Probing a device that has no media (an empty card reader or optical drive) otherwise raises
    // a "There is no disk in the drive" dialog owned by whichever thread issued the call.
    public const uint SEM_FAILCRITICALERRORS = 0x0001;

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern unsafe bool SetThreadErrorMode(uint dwNewMode, uint* lpOldMode);
}
