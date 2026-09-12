namespace Task.Monitor.Interop.Win32;

public static class NetIoApi
{
    public const uint IF_TYPE_OTHER              = 1;
    public const uint IF_TYPE_ETHERNET_CSMACD    = 6;
    public const uint IF_TYPE_ISO88025_TOKENRING = 9;
    public const uint IF_TYPE_PPP                = 23;
    public const uint IF_TYPE_SOFTWARE_LOOPBACK  = 24;
    public const uint IF_TYPE_ATM                = 37;
    // What a VPN client adapter reports. Observed on a Private Internet Access adapter, which
    // presents as PROP_VIRTUAL rather than TUNNEL.
    public const uint IF_TYPE_PROP_VIRTUAL       = 53;
    public const uint IF_TYPE_IEEE80211          = 71;
    public const uint IF_TYPE_TUNNEL             = 131;
    public const uint IF_TYPE_IEEE1394           = 144;
    public const uint IF_TYPE_IEEE80216_WMAN     = 237;
    public const uint IF_TYPE_WWANPP             = 243;
    public const uint IF_TYPE_WWANPP2            = 244;

    public const uint IfOperStatusUp             = 1;
    public const uint IfOperStatusDown           = 2;
    public const uint IfOperStatusTesting        = 3;
    public const uint IfOperStatusUnknown        = 4;
    public const uint IfOperStatusDormant        = 5;
    public const uint IfOperStatusNotPresent     = 6;
    public const uint IfOperStatusLowerLayerDown = 7;

    public const uint NdisPhysicalMediumUnspecified   = 0;
    public const uint NdisPhysicalMediumWirelessLan   = 1;
    public const uint NdisPhysicalMediumCableModem    = 2;
    public const uint NdisPhysicalMediumPhoneLine     = 3;
    public const uint NdisPhysicalMediumPowerLine     = 4;
    public const uint NdisPhysicalMediumDSL           = 5;
    public const uint NdisPhysicalMediumFibreChannel  = 6;
    public const uint NdisPhysicalMedium1394          = 7;
    public const uint NdisPhysicalMediumWirelessWan   = 8;
    public const uint NdisPhysicalMediumNative802_11  = 9;
    public const uint NdisPhysicalMediumBluetooth     = 10;
    public const uint NdisPhysicalMediumInfiniband    = 11;
    public const uint NdisPhysicalMediumWiMax         = 12;
    public const uint NdisPhysicalMediumUWB           = 13;
    public const uint NdisPhysicalMedium802_3         = 14;
    public const uint NdisPhysicalMedium802_5         = 15;
    public const uint NdisPhysicalMediumIrda          = 16;
    public const uint NdisPhysicalMediumWiredWAN      = 17;
    public const uint NdisPhysicalMediumWiredCoWan    = 18;
    public const uint NdisPhysicalMediumOther         = 19;

    // MIB_IF_TABLE2: ULONG NumEntries then MIB_IF_ROW2 Table[ANY_SIZE]. The row array is eight
    // byte aligned because MIB_IF_ROW2 leads with a ULONG64 LUID.
    public const int IfTable2NumEntriesOffset = 0x00;
    public const int IfTable2TableOffset      = 0x08;

    // MIB_IF_ROW2, 1352 bytes on x64. Only the identity and the octet/packet counters are read,
    // so the layout is expressed as offsets rather than marshalled through a 1.3KB struct.
    public const int IfRow2Size                      = 1352;
    public const int IfRow2InterfaceLuidOffset       = 0;
    public const int IfRow2InterfaceIndexOffset      = 8;
    public const int IfRow2AliasOffset               = 28;
    public const int IfRow2DescriptionOffset         = 542;
    public const int IfRow2StringLength              = 257;
    public const int IfRow2PhysicalAddressLenOffset  = 1056;
    public const int IfRow2PhysicalAddressOffset     = 1060;
    public const int IfRow2MaxPhysicalAddressLength  = 32;
    public const int IfRow2TypeOffset                = 1128;
    public const int IfRow2PhysicalMediumTypeOffset  = 1140;
    public const int IfRow2OperStatusOffset          = 1156;
    public const int IfRow2TransmitLinkSpeedOffset   = 1192;
    public const int IfRow2ReceiveLinkSpeedOffset    = 1200;
    public const int IfRow2InOctetsOffset            = 1208;
    public const int IfRow2InUcastPktsOffset         = 1216;
    public const int IfRow2OutOctetsOffset           = 1280;
    public const int IfRow2OutUcastPktsOffset        = 1288;
}
