#if __APPLE__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static partial class ErrorCodes
{
    private static string? DescribeLarge(uint code)
    {
        string message = MachErrorString(unchecked((int)code));

        return message.Contains("unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : message;
    }

    private static string MachErrorString(int code)
    {
        nint text = MachError.mach_error_string(code);

        return text == nint.Zero
            ? string.Empty
            : Normalise(Marshal.PtrToStringUTF8(text) ?? string.Empty);
    }
}
#endif
