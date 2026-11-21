#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

public static class FILETIMEExtensions
{
    public static long ToLong(this MinWinBase.FILETIME fileTime) =>
        (long)(((ulong)fileTime.dwHighDateTime << 32) + fileTime.dwLowDateTime);
}
#endif
