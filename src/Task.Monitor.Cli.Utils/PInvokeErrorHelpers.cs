using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Task.Monitor.Cli.Utils;

public static class PInvokeErrorHelpers
{
    public static void AssertOnLastError(string message)
    {
#if DEBUG
        int error = Marshal.GetLastPInvokeError();
        Debug.Assert(error == 0, GetFormattedErrorMessage(message));
#endif
    }

    public static void TraceOnLastError(string message) =>
        Trace.WriteLine(GetFormattedErrorMessage(message));

    public static void TraceOnceOnLastError(string key) =>
        TraceEx.WriteLineOnce(key, GetFormattedErrorMessage());

    public static void TraceOnceOnLastError(string key, string message) => 
        TraceEx.WriteLineOnce(key, GetFormattedErrorMessage(message));

    public static void TraceOnPInvokeError(string message, uint error) =>
        Trace.WriteLine($"PINVOKE ERROR ({error}): {message}");

    public static void TraceOnceOnPInvokeError(string key, string message, uint error) =>
        TraceEx.WriteLineOnce(key, $"PINVOKE ERROR ({error}): {message}");

    private static string GetFormattedErrorMessage() => GetFormattedErrorMessage(string.Empty);
    
    private static string GetFormattedErrorMessage(string message)
    {
        int error = Marshal.GetLastPInvokeError();
        string errorMessage = $"PINVOKE ERROR ({error}): {Marshal.GetPInvokeErrorMessage(error)}";

        if (string.IsNullOrEmpty(message)) {
            errorMessage += $" {message}";
        }
        
        return errorMessage;
    }
}
