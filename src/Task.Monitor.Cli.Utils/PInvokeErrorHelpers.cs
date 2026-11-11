using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Task.Monitor.Cli.Utils;

public static class PInvokeErrorHelpers
{
    private const string HResultError = "HRESULT ERROR";
    private const string PInvokeError = "PINVOKE ERROR";

    public static void AssertOnLastError(string message)
    {
#if DEBUG
        int error = Marshal.GetLastPInvokeError();
        Debug.Assert(error == 0, GetFormattedErrorMessage(message));
#endif
    }

    private static string Format(string kind, string description, string message) =>
        string.IsNullOrEmpty(message)
            ? $"{kind} {description}"
            : $"{kind} {description}: {message}";

    private static string GetFormattedErrorMessage(string message)
    {
        int error = Marshal.GetLastPInvokeError();

        return Format(PInvokeError, ErrorCodes.Describe(error), message);
    }

    public static void TraceOnceOnHResult(string key, string message, int hr) =>
        TraceEx.WriteLineOnce(key, Format(HResultError, ErrorCodes.Describe(hr), message));

    public static void TraceOnceOnLastError(string key) =>
        TraceEx.WriteLineOnce(key, GetFormattedErrorMessage(string.Empty));

    public static void TraceOnceOnLastError(string key, string message) =>
        TraceEx.WriteLineOnce(key, GetFormattedErrorMessage(message));

    public static void TraceOnceOnPInvokeError(string key, string message, uint error) =>
        TraceEx.WriteLineOnce(key, Format(PInvokeError, ErrorCodes.Describe(error), message));

    public static void TraceOnHResult(string message, int hr) =>
        Trace.WriteLine(Format(HResultError, ErrorCodes.Describe(hr), message));

    public static void TraceOnLastError(string message) =>
        Trace.WriteLine(GetFormattedErrorMessage(message));

    public static void TraceOnPInvokeError(string message, uint error) =>
        Trace.WriteLine(Format(PInvokeError, ErrorCodes.Describe(error), message));
}
