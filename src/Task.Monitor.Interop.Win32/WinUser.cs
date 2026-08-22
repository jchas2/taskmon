using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class WinUser
{
    public const uint GR_GDIOBJECTS = 0;
    public const uint GR_USEROBJECTS = 1;
    
    [DllImport(Libraries.User32)]
    public static extern uint GetGuiResources(nint hProcess, uint uiFlags);
}