using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class WinUser
{
    public const uint GR_GDIOBJECTS = 0;
    public const uint GR_USEROBJECTS = 1;

    [ThreadStatic]
    private static List<nint>? enumeratedWindows;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int CollectWindow(nint window, nint parameter)
    {
        enumeratedWindows?.Add(window);
        return 1;
    }

    [DllImport(Libraries.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool EnumWindows(delegate* unmanaged[Stdcall]<nint, nint, int> lpEnumFunc, nint lParam);

    [DllImport(Libraries.User32, SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern unsafe int GetClassNameW(nint hWnd, char* lpClassName, int nMaxCount);

    [DllImport(Libraries.User32)]
    public static extern uint GetGuiResources(nint hProcess, uint uiFlags);

    public static unsafe IReadOnlyList<nint> GetTopLevelWindows()
    {
        enumeratedWindows = new List<nint>();

        try {
            EnumWindows(&CollectWindow, nint.Zero);
            return enumeratedWindows;
        }
        finally {
            enumeratedWindows = null;
        }
    }

    public static unsafe string GetWindowClassName(nint window)
    {
        const int MaxClassNameLength = 256;

        char* buffer = stackalloc char[MaxClassNameLength];
        int   length = GetClassNameW(window, buffer, MaxClassNameLength);

        return new string(buffer, 0, Math.Max(0, length));
    }

    [DllImport(Libraries.User32, SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport(Libraries.User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsHungAppWindow(nint hWnd);

    [DllImport(Libraries.User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(nint hWnd);
}
