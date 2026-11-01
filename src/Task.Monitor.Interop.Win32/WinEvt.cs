using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class WinEvt
{
    public const uint ERROR_INSUFFICIENT_BUFFER = 122;
    public const uint ERROR_NO_MORE_ITEMS       = 259;

    public const uint EvtQueryChannelPath      = 0x1;
    public const uint EvtQueryReverseDirection = 0x200;
    public const uint EvtRenderEventXml        = 1;

    public const uint Infinite = 0xFFFFFFFF;

    [DllImport(Libraries.WevtApi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EvtClose(nint handle);

    [DllImport(Libraries.WevtApi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool EvtNext(
        nint  resultSet,
        uint  eventsSize,
        nint* events,
        uint  timeout,
        uint  flags,
        uint* returned);

    [DllImport(Libraries.WevtApi, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern nint EvtQuery(
        nint   session, 
        string path, 
        string query, 
        uint   flags);

    [DllImport(Libraries.WevtApi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool EvtRender(
        nint  context,
        nint  fragment,
        uint  flags,
        uint  bufferSize,
        char* buffer,
        uint* bufferUsed,
        uint* propertyCount);
}
