using System.Runtime.InteropServices;
using System.Text;

namespace Task.Manager.Interop.Win32;

// Following declarations are found in the platform sdk header file MinWinBase.h

public static class WinBase
{
    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Auto)] 
    public static extern bool GetComputerName(IntPtr lpBuffer, ref uint lpnSize);
}
