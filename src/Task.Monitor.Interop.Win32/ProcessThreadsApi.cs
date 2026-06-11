using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Task.Monitor.Interop.Win32;

public static class ProcessThreadsApi
{
    [DllImport(Libraries.Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool GetSystemTimes(
        MinWinBase.FILETIME* lpIdleTime,
        MinWinBase.FILETIME* lpKernelTime,
        MinWinBase.FILETIME* lpUserTime);
    
    [DllImport(Libraries.Advapi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(
        SafeProcessHandle processHandle,
        uint desiredAccess,
        out SafeProcessHandle tokenHandle);
}
