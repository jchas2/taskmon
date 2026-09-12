namespace Task.Monitor.Interop.Win32;

public static class WS2Def
{
    public const uint   AF_UNSPEC = 0;
    public const ushort AF_INET   = 2;
    public const ushort AF_INET6  = 23;

    // struct sockaddr_in  { short sin_family; u_short sin_port; in_addr sin_addr; char sin_zero[8]; }
    // struct sockaddr_in6 { short sin6_family; u_short sin6_port; u_long sin6_flowinfo;
    //                       in6_addr sin6_addr; u_long sin6_scope_id; }
    // Only the family and the address bytes are read, so the layout is expressed as offsets.
    public const int SockAddrFamilyOffset   = 0x00;
    public const int SockAddrIn4DataOffset  = 0x04;
    public const int SockAddrIn4DataLength  = 4;
    public const int SockAddrIn6DataOffset  = 0x08;
    public const int SockAddrIn6DataLength  = 16;
    public const int SockAddrIn6ScopeOffset = 0x18;

    // typedef struct _SOCKET_ADDRESS { LPSOCKADDR lpSockaddr; INT iSockaddrLength; }
    public const int SocketAddressSockAddrOffset = 0x00;
    public const int SocketAddressLengthOffset   = 0x08;
}
