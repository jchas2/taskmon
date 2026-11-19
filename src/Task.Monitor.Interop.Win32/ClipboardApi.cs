using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

// The clipboard functions from winuser.h and the global memory they hand over, from winbase.h.
public static class ClipboardApi
{
    public const uint CF_UNICODETEXT = 13;
    public const uint GMEM_MOVEABLE  = 0x0002;

    [DllImport(Libraries.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseClipboard();

    [DllImport(Libraries.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EmptyClipboard();

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern nint GlobalFree(nint hMem);

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern nint GlobalLock(nint hMem);

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalUnlock(nint hMem);

    [DllImport(Libraries.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport(Libraries.User32, SetLastError = true)]
    public static extern nint SetClipboardData(uint uFormat, nint hMem);
}
