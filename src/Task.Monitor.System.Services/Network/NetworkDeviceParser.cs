using System.Net;
using System.Text;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Network;

public static class NetworkDeviceParser
{
    public const string NotAvailable = "N/A";

    // A disconnected adapter reports either zero or the all ones sentinel for link speed.
    private const ulong UnknownLinkSpeed = ulong.MaxValue;

    public static string DecodeConnectionType(uint ifType) => ifType switch
    {
        NetIoApi.IF_TYPE_ETHERNET_CSMACD    => "Ethernet",
        NetIoApi.IF_TYPE_IEEE80211          => "Wi-Fi",
        NetIoApi.IF_TYPE_PPP                => "PPP",
        NetIoApi.IF_TYPE_TUNNEL             => "Tunnel",
        NetIoApi.IF_TYPE_PROP_VIRTUAL       => "Virtual",
        NetIoApi.IF_TYPE_SOFTWARE_LOOPBACK  => "Loopback",
        NetIoApi.IF_TYPE_IEEE1394           => "IEEE 1394",
        NetIoApi.IF_TYPE_ATM                => "ATM",
        NetIoApi.IF_TYPE_ISO88025_TOKENRING => "Token Ring",
        NetIoApi.IF_TYPE_IEEE80216_WMAN     => "WiMAX",
        NetIoApi.IF_TYPE_WWANPP             => "Mobile Broadband (GSM)",
        NetIoApi.IF_TYPE_WWANPP2            => "Mobile Broadband (CDMA)",
        NetIoApi.IF_TYPE_OTHER              => "Other",
        _                                   => NotAvailable,
    };

    public static string DecodePhysicalMedium(uint physicalMediumType) => physicalMediumType switch
    {
        NetIoApi.NdisPhysicalMedium802_3        => "802.3",
        NetIoApi.NdisPhysicalMediumNative802_11 => "Native 802.11",
        NetIoApi.NdisPhysicalMediumWirelessLan  => "Wireless LAN",
        NetIoApi.NdisPhysicalMediumWirelessWan  => "Wireless WAN",
        NetIoApi.NdisPhysicalMediumBluetooth    => "Bluetooth",
        NetIoApi.NdisPhysicalMediumCableModem   => "Cable Modem",
        NetIoApi.NdisPhysicalMediumPhoneLine    => "Phone Line",
        NetIoApi.NdisPhysicalMediumPowerLine    => "Power Line",
        NetIoApi.NdisPhysicalMediumDSL          => "DSL",
        NetIoApi.NdisPhysicalMediumFibreChannel => "Fibre Channel",
        NetIoApi.NdisPhysicalMedium1394         => "IEEE 1394",
        NetIoApi.NdisPhysicalMediumInfiniband   => "InfiniBand",
        NetIoApi.NdisPhysicalMediumWiMax        => "WiMAX",
        NetIoApi.NdisPhysicalMediumUWB          => "Ultra Wideband",
        NetIoApi.NdisPhysicalMedium802_5        => "802.5",
        NetIoApi.NdisPhysicalMediumIrda         => "IrDA",
        NetIoApi.NdisPhysicalMediumWiredWAN     => "Wired WAN",
        NetIoApi.NdisPhysicalMediumWiredCoWan   => "Wired CoWAN",
        NetIoApi.NdisPhysicalMediumOther        => "Other",
        _                                       => NotAvailable,
    };

    public static string DecodeOperationalStatus(uint operStatus) => operStatus switch
    {
        NetIoApi.IfOperStatusUp             => "Up",
        NetIoApi.IfOperStatusDown           => "Down",
        NetIoApi.IfOperStatusTesting        => "Testing",
        NetIoApi.IfOperStatusDormant        => "Dormant",
        NetIoApi.IfOperStatusNotPresent     => "Not Present",
        NetIoApi.IfOperStatusLowerLayerDown => "Lower Layer Down",
        NetIoApi.IfOperStatusUnknown        => "Unknown",
        _                                   => NotAvailable,
    };

    // An adapter is worth monitoring when it is up, is not the loopback pseudo interface, and
    // holds at least one address. On a typical machine that reduces a list of half a dozen
    // adapters (VPN, Bluetooth PAN, an idle Wi-Fi radio) to the one actually carrying traffic.
    public static bool IsActiveAdapter(uint ifType, uint operStatus, int addressCount) =>
        operStatus == NetIoApi.IfOperStatusUp &&
        ifType != NetIoApi.IF_TYPE_SOFTWARE_LOOPBACK &&
        addressCount > 0;

    // An overlay adapter sits on top of a physical adapter and reports the same bytes a second
    // time, so including it in the aggregate would count everything sent over the VPN twice.
    //
    // PROP_VIRTUAL matters as much as TUNNEL here: a Private Internet Access adapter on this
    // machine reports type 53, not 131, so excluding only tunnels would still double count it.
    public static bool CountsTowardAggregate(uint ifType, bool isActive) =>
        isActive &&
        ifType != NetIoApi.IF_TYPE_TUNNEL &&
        ifType != NetIoApi.IF_TYPE_PROP_VIRTUAL &&
        ifType != NetIoApi.IF_TYPE_PPP;

    public static ulong DecodeLinkSpeed(ulong linkSpeed) =>
        linkSpeed == UnknownLinkSpeed ? 0 : linkSpeed;

    public static string FormatMacAddress(ReadOnlySpan<byte> physicalAddress)
    {
        if (physicalAddress.IsEmpty) {
            return string.Empty;
        }

        StringBuilder builder = new(physicalAddress.Length * 3);

        for (int i = 0; i < physicalAddress.Length; i++) {
            if (i > 0) {
                builder.Append('-');
            }

            builder.Append(physicalAddress[i].ToString("X2"));
        }

        return builder.ToString();
    }

    // The raw sockaddr bytes come from native enumeration; IPAddress is used only to render them.
    // Hand rolling RFC 5952 IPv6 zero compression is easy to get subtly wrong and this keeps the
    // formatting testable.
    public static string FormatIPv4Address(ReadOnlySpan<byte> addressBytes) =>
        addressBytes.Length != WS2Def.SockAddrIn4DataLength
            ? string.Empty
            : new IPAddress(addressBytes).ToString();

    public static string FormatIPv6Address(ReadOnlySpan<byte> addressBytes, uint scopeId) =>
        addressBytes.Length != WS2Def.SockAddrIn6DataLength
            ? string.Empty
            : new IPAddress(addressBytes, scopeId).ToString();
}
