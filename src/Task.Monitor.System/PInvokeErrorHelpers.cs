using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Task.Monitor.System;

public static class PInvokeErrorHelpers
{
    public static void AssertOnLastError(string methodName)
    {
#if DEBUG
        int error = Marshal.GetLastPInvokeError();
        Debug.Assert(error == 0, $"Failed with {GetFormattedErrorMesage()}");
#endif
    }

    public static string GetFormattedErrorMesage()
    {
        int error = Marshal.GetLastPInvokeError();
        string message = Marshal.GetPInvokeErrorMessage(error);
        return $"PInvoke error ({error}): {message}";
    }
}
