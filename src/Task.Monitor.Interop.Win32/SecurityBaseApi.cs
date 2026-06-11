using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Task.Monitor.Interop.Win32;

public static class SecurityBaseApi
{
    [DllImport(Libraries.Advapi32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern unsafe bool GetTokenInformation(
        SafeProcessHandle tokenHandle,
        uint tokenInformationClass,
        uint* tokenInformation,
        int tokenInformationLength,
        uint* returnLength);
}
