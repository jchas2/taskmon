#if __APPLE__
using System.Runtime.InteropServices;

namespace Task.Monitor.Cli.Utils;

public static unsafe partial class ErrorCodes
{
    private const string LibSystem = "/usr/lib/libSystem.dylib";

    private static string? DescribeLarge(uint code)
    {
        string message = MachErrorString(unchecked((int)code));

        return message.Contains("unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : message;
    }

    private static string MachErrorString(int code)
    {
        nint text = mach_error_string(code);

        return text == nint.Zero
            ? string.Empty
            : Normalise(Marshal.PtrToStringUTF8(text) ?? string.Empty);
    }

    [DllImport(LibSystem)]
    private static extern nint mach_error_string(int errorValue);
}
#endif
