#if __WIN32__
using System.Runtime.InteropServices;

namespace Task.Monitor.Cli.Utils;

public static unsafe partial class ErrorCodes
{
    private const uint FORMAT_MESSAGE_FROM_HMODULE   = 0x00000800;
    private const uint FORMAT_MESSAGE_FROM_SYSTEM    = 0x00001000;
    private const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
    private const uint LOAD_LIBRARY_AS_DATAFILE      = 0x00000002;

    private const int MessageCapacity = 1024;

    private static readonly string[]     MessageModulePaths = [@"pdh.dll", @"wbem\wmiutils.dll", @"ntdll.dll"];
    private static readonly Lazy<nint[]> MessageModules     = new(LoadMessageModules);

    private static string? DescribeLarge(uint code)
    {
        string? text = FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM, nint.Zero, code);

        if (text != null) {
            return text;
        }

        foreach (nint module in MessageModules.Value) {
            if (module == nint.Zero) {
                continue;
            }

            text = FormatMessage(FORMAT_MESSAGE_FROM_HMODULE, module, code);

            if (text != null) {
                return text;
            }
        }

        return null;
    }

    private static string? FormatMessage(uint source, nint module, uint code)
    {
        char* buffer = stackalloc char[MessageCapacity];

        uint length = FormatMessageW(
            source | FORMAT_MESSAGE_IGNORE_INSERTS,
            module,
            code,
            languageId: 0,
            buffer,
            MessageCapacity,
            arguments: nint.Zero);

        return length == 0
            ? null
            : Normalise(new string(buffer, 0, (int)length));
    }

    [DllImport("kernel32.dll")]
    private static extern uint FormatMessageW(
        uint  flags,
        nint  source,
        uint  messageId,
        uint  languageId,
        char* buffer,
        int   size,
        nint  arguments);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadLibraryExW(string fileName, nint file, uint flags);

    private static nint[] LoadMessageModules() =>
        MessageModulePaths
            .Select(path => LoadLibraryExW(
                Path.Combine(Environment.SystemDirectory, path), 
                nint.Zero, 
                LOAD_LIBRARY_AS_DATAFILE))
            .ToArray();
}
#endif
