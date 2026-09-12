namespace Task.Monitor.Interop.Win32;

public static class IpTypes
{
    // IP_ADAPTER_ADDRESSES_LH is a linked list node with a large tail this code never reads, and
    // its layout is version dependent past the fields below. It is walked as a byte span using
    // offsets rather than marshalled, in the same style as the storage descriptor decode.
    //
    //   0x00 ULONG   Length            (union with ULONGLONG Alignment)
    //   0x04 ULONG   IfIndex
    //   0x08 PTR     Next
    //   0x10 PCHAR   AdapterName       (ANSI)
    //   0x18 PTR     FirstUnicastAddress
    //   0x20 PTR     FirstAnycastAddress
    //   0x28 PTR     FirstMulticastAddress
    //   0x30 PTR     FirstDnsServerAddress
    //   0x38 PWCHAR  DnsSuffix
    //   0x40 PWCHAR  Description
    //   0x48 PWCHAR  FriendlyName
    //   0x50 BYTE[8] PhysicalAddress
    //   0x58 ULONG   PhysicalAddressLength
    //   0x5C ULONG   Flags
    //   0x60 ULONG   Mtu
    //   0x64 ULONG   IfType
    //   0x68 INT     OperStatus
    //   0x6C ULONG   Ipv6IfIndex
    //   0x70 ULONG[16] ZoneIndices
    //   0xB0 PTR     FirstPrefix
    //   0xB8 ULONG64 TransmitLinkSpeed
    //   0xC0 ULONG64 ReceiveLinkSpeed
    //   0xC8 PTR     FirstWinsServerAddress
    //   0xD0 PTR     FirstGatewayAddress
    //   0xD8 ULONG   Ipv4Metric
    //   0xDC ULONG   Ipv6Metric
    //   0xE0 ULONG64 Luid
    public const int AdapterAddressesIfIndexOffset              = 0x04;
    public const int AdapterAddressesNextOffset                 = 0x08;
    public const int AdapterAddressesAdapterNameOffset          = 0x10;
    public const int AdapterAddressesFirstUnicastAddressOffset  = 0x18;
    public const int AdapterAddressesDescriptionOffset          = 0x40;
    public const int AdapterAddressesFriendlyNameOffset         = 0x48;
    public const int AdapterAddressesPhysicalAddressOffset      = 0x50;
    public const int AdapterAddressesPhysicalAddressLenOffset   = 0x58;
    public const int AdapterAddressesIfTypeOffset               = 0x64;
    public const int AdapterAddressesOperStatusOffset           = 0x68;
    public const int AdapterAddressesTransmitLinkSpeedOffset    = 0xB8;
    public const int AdapterAddressesReceiveLinkSpeedOffset     = 0xC0;
    public const int AdapterAddressesLuidOffset                 = 0xE0;
    public const int AdapterAddressesMinimumLength              = 0xE8;

    public const int MaxAdapterAddressLength = 8;

    // IP_ADAPTER_UNICAST_ADDRESS_LH
    //   0x00 ULONG          Length
    //   0x04 DWORD          Flags
    //   0x08 PTR            Next
    //   0x10 SOCKET_ADDRESS Address
    public const int UnicastAddressNextOffset    = 0x08;
    public const int UnicastAddressAddressOffset = 0x10;
    public const int UnicastAddressMinimumLength = 0x20;
}
