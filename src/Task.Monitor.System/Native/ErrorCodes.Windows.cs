#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

public static unsafe partial class ErrorCodes
{
    private const int MessageCapacity = 1024;

    // Codes that FormatMessage can't describe from the system table come from these components.
    private static readonly string[]     MessageModulePaths = [@"pdh.dll", @"wbem\wmiutils.dll", @"ntdll.dll"];
    private static readonly Lazy<nint[]> MessageModules     = new(LoadMessageModules);

    private static string? DescribeLarge(uint code)
    {
        string? text = FormatMessage(WinBase.FORMAT_MESSAGE_FROM_SYSTEM, nint.Zero, code);

        if (text != null) {
            return text;
        }

        foreach (nint module in MessageModules.Value) {
            if (module == nint.Zero) {
                continue;
            }

            text = FormatMessage(WinBase.FORMAT_MESSAGE_FROM_HMODULE, module, code);

            if (text != null) {
                return text;
            }
        }

        return null;
    }

    private static string? FormatMessage(uint source, nint module, uint code)
    {
        char* buffer = stackalloc char[MessageCapacity];

        uint length = WinBase.FormatMessageW(
            source | WinBase.FORMAT_MESSAGE_IGNORE_INSERTS,
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

    private static nint[] LoadMessageModules() =>
        MessageModulePaths
            .Select(path => LibLoaderApi.LoadLibraryExW(
                Path.Combine(Environment.SystemDirectory, path),
                nint.Zero,
                LibLoaderApi.LOAD_LIBRARY_AS_DATAFILE))
            .ToArray();
}
#endif
