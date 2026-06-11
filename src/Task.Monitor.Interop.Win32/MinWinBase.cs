using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class MinWinBase
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }
}
