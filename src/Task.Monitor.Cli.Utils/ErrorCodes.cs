using System.Runtime.InteropServices;
using System.Text;

namespace Task.Monitor.Cli.Utils;

public static unsafe partial class ErrorCodes
{
    private const uint LargestSmallCode = 0xFFFF;

    private static readonly char[] LineBreaks = ['\r', '\n'];

    public static string Describe(int code) => Describe(unchecked((uint)code));

    public static string Describe(uint code)
    {
        if (code <= LargestSmallCode) {
            return $"{code} ({Normalise(Marshal.GetPInvokeErrorMessage((int)code))})";
        }

        string? text = DescribeLarge(code);

        return string.IsNullOrEmpty(text)
            ? $"0x{code:X8}"
            : $"0x{code:X8} ({text})";
    }

    public static string DescribeKernReturn(int code)
    {
        string number = unchecked((uint)code) <= LargestSmallCode ? $"{code}" : $"0x{code:X8}";

#if __APPLE__
        string message = MachErrorString(code);

        return message.Length == 0
            ? number
            : $"{number} ({message})";
#else
        return number;
#endif
    }

#if !__WIN32__ && !__APPLE__
    private static string? DescribeLarge(uint code) => null;
#endif

    private static string Normalise(string text)
    {
        StringBuilder builder = new(text.Length);

        foreach (string part in text.Split(LineBreaks, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            if (builder.Length > 0) {
                builder.Append(' ');
            }

            builder.Append(part);
        }

        return builder.ToString();
    }
}
