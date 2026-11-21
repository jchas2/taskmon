using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static unsafe class WinBase
{
    public const uint FORMAT_MESSAGE_FROM_HMODULE   = 0x00000800;
    public const uint FORMAT_MESSAGE_FROM_SYSTEM    = 0x00001000;
    public const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;

    [DllImport(Libraries.Kernel32)]
    public static extern uint FormatMessageW(
        uint  flags,
        nint  source,
        uint  messageId,
        uint  languageId,
        char* buffer,
        int   size,
        nint  arguments);
}
