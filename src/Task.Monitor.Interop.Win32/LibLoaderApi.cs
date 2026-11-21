using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class LibLoaderApi
{
    public const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;

    [DllImport(Libraries.Kernel32, CharSet = CharSet.Unicode)]
    public static extern nint LoadLibraryExW(string fileName, nint file, uint flags);
}
