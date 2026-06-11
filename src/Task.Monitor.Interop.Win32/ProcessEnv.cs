using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class ProcessEnv
{
    public const int STD_OUTPUT_HANDLE = -11;
    public const int STD_INPUT_HANDLE = -10;

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern IntPtr GetStdHandle(int nStdHandle);
}
