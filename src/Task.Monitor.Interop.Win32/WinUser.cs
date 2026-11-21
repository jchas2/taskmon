using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class WinUser
{
    public const uint GR_GDIOBJECTS = 0;
    public const uint GR_USEROBJECTS = 1;

    [DllImport(Libraries.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool EnumWindows(delegate* unmanaged[Stdcall]<nint, nint, int> lpEnumFunc, nint lParam);

    [DllImport(Libraries.User32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe int GetClassNameW(nint hWnd, char* lpClassName, int nMaxCount);

    [DllImport(Libraries.User32)]
    public static extern uint GetGuiResources(nint hProcess, uint uiFlags);

    [DllImport(Libraries.User32, SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport(Libraries.User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsHungAppWindow(nint hWnd);

    [DllImport(Libraries.User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(nint hWnd);
}
