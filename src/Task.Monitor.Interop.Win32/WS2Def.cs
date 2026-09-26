namespace Task.Monitor.Interop.Win32;

public static class WS2Def
{
    public const uint   AF_UNSPEC = 0;
    public const ushort AF_INET   = 2;
    public const ushort AF_INET6  = 23;

    // Offsets into sockaddr_in and sockaddr_in6:
    public const int SockAddrFamilyOffset   = 0x00;
    public const int SockAddrIn4DataOffset  = 0x04;
    public const int SockAddrIn4DataLength  = 4;
    public const int SockAddrIn6DataOffset  = 0x08;
    public const int SockAddrIn6DataLength  = 16;
    public const int SockAddrIn6ScopeOffset = 0x18;

    // typedef struct _SOCKET_ADDRESS:
    public const int SocketAddressSockAddrOffset = 0x00;
    public const int SocketAddressLengthOffset   = 0x08;
}
