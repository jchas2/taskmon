using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Task.Monitor.System;

public static class Win32ErrorHelpers
{
    public static void AssertOnLastError(string methodName)
    {
#if DEBUG
        int error = Marshal.GetLastWin32Error();
        Debug.Assert(error == 0, $"Failed {methodName}: {Marshal.GetPInvokeErrorMessage(error)}");
#endif
    }
}