using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class IpHlpApi
{
    public const uint ERROR_SUCCESS         = 0;
    public const uint ERROR_BUFFER_OVERFLOW = 111;
    public const uint ERROR_NO_DATA         = 232;

    public const uint GAA_FLAG_SKIP_UNICAST       = 0x0001;
    public const uint GAA_FLAG_SKIP_ANYCAST       = 0x0002;
    public const uint GAA_FLAG_SKIP_MULTICAST     = 0x0004;
    public const uint GAA_FLAG_SKIP_DNS_SERVER    = 0x0008;
    public const uint GAA_FLAG_INCLUDE_PREFIX     = 0x0010;
    public const uint GAA_FLAG_SKIP_FRIENDLY_NAME = 0x0020;

    [DllImport(Libraries.IpHlpApi, SetLastError = true)]
    public static extern unsafe uint GetAdaptersAddresses(
        uint  family,
        uint  flags,
        nint  reserved,
        void* adapterAddresses,
        uint* sizePointer);

    // Allocates the table itself and hands back a pointer to it, so every path out of a caller
    // has to reach FreeMibTable.
    [DllImport(Libraries.IpHlpApi)]
    public static extern unsafe uint GetIfTable2(nint* table);

    [DllImport(Libraries.IpHlpApi)]
    public static extern void FreeMibTable(nint memory);
}
