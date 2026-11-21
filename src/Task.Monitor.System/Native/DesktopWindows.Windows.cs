#if __WIN32__
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

public static unsafe class DesktopWindows
{
    private const int MaxClassNameLength = 256;

    // EnumWindows calls back into a static method, so the windows it finds are gathered here.
    [ThreadStatic]
    private static List<nint>? enumeratedWindows;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int CollectWindow(nint window, nint parameter)
    {
        enumeratedWindows?.Add(window);
        return 1;
    }

    public static string GetClassName(nint window)
    {
        char* buffer = stackalloc char[MaxClassNameLength];
        int   length = WinUser.GetClassNameW(window, buffer, MaxClassNameLength);

        return new string(buffer, 0, Math.Max(0, length));
    }

    public static IReadOnlyList<nint> GetTopLevel()
    {
        enumeratedWindows = new List<nint>();

        try {
            WinUser.EnumWindows(&CollectWindow, nint.Zero);
            return enumeratedWindows;
        }
        finally {
            enumeratedWindows = null;
        }
    }
}
#endif
