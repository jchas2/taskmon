using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Network;

namespace Task.Monitor.System.Services.Tests.Network;

public sealed class NetworkDeviceParserTests
{
    [Theory]
    [InlineData(NetIoApi.IF_TYPE_ETHERNET_CSMACD, "Ethernet")]
    [InlineData(NetIoApi.IF_TYPE_IEEE80211, "Wi-Fi")]
    [InlineData(NetIoApi.IF_TYPE_TUNNEL, "Tunnel")]
    [InlineData(NetIoApi.IF_TYPE_PROP_VIRTUAL, "Virtual")]
    [InlineData(NetIoApi.IF_TYPE_PPP, "PPP")]
    [InlineData(NetIoApi.IF_TYPE_SOFTWARE_LOOPBACK, "Loopback")]
    [InlineData(9999u, NetworkDeviceParser.NotAvailable)]
    public void Should_Decode_Connection_Type(uint ifType, string expected) =>
        Assert.Equal(expected, NetworkDeviceParser.DecodeConnectionType(ifType));

    [Theory]
    [InlineData(NetIoApi.NdisPhysicalMedium802_3, "802.3")]
    [InlineData(NetIoApi.NdisPhysicalMediumNative802_11, "Native 802.11")]
    [InlineData(NetIoApi.NdisPhysicalMediumBluetooth, "Bluetooth")]
    [InlineData(NetIoApi.NdisPhysicalMediumUnspecified, NetworkDeviceParser.NotAvailable)]
    public void Should_Decode_Physical_Medium(uint physicalMediumType, string expected) =>
        Assert.Equal(expected, NetworkDeviceParser.DecodePhysicalMedium(physicalMediumType));

    [Theory]
    [InlineData(NetIoApi.IfOperStatusUp, "Up")]
    [InlineData(NetIoApi.IfOperStatusDown, "Down")]
    [InlineData(NetIoApi.IfOperStatusLowerLayerDown, "Lower Layer Down")]
    public void Should_Decode_Operational_Status(uint operStatus, string expected) =>
        Assert.Equal(expected, NetworkDeviceParser.DecodeOperationalStatus(operStatus));

    [Fact]
    public void Should_Treat_An_Up_Adapter_With_An_Address_As_Active() =>
        Assert.True(NetworkDeviceParser.IsActiveAdapter(
            NetIoApi.IF_TYPE_ETHERNET_CSMACD, NetIoApi.IfOperStatusUp, addressCount: 1));

    [Fact]
    public void Should_Not_Treat_A_Down_Adapter_As_Active() =>
        Assert.False(NetworkDeviceParser.IsActiveAdapter(
            NetIoApi.IF_TYPE_ETHERNET_CSMACD, NetIoApi.IfOperStatusDown, addressCount: 1));

    [Fact]
    public void Should_Not_Treat_Loopback_As_Active() =>
        // Loopback reports itself as up and holds 127.0.0.1, so it has to be excluded by type.
        Assert.False(NetworkDeviceParser.IsActiveAdapter(
            NetIoApi.IF_TYPE_SOFTWARE_LOOPBACK, NetIoApi.IfOperStatusUp, addressCount: 2));

    [Fact]
    public void Should_Not_Treat_An_Adapter_Without_An_Address_As_Active() =>
        Assert.False(NetworkDeviceParser.IsActiveAdapter(
            NetIoApi.IF_TYPE_ETHERNET_CSMACD, NetIoApi.IfOperStatusUp, addressCount: 0));

    [Fact]
    public void Should_Count_A_Physical_Adapter_Toward_The_Aggregate() =>
        Assert.True(NetworkDeviceParser.CountsTowardAggregate(
            NetIoApi.IF_TYPE_ETHERNET_CSMACD, isActive: true));

    [Theory]
    [InlineData(NetIoApi.IF_TYPE_TUNNEL)]
    [InlineData(NetIoApi.IF_TYPE_PROP_VIRTUAL)]
    [InlineData(NetIoApi.IF_TYPE_PPP)]
    public void Should_Keep_Overlay_Adapters_Out_Of_The_Aggregate(uint ifType) =>
        // An overlay adapter reports the same bytes as the adapter underneath it. A Private
        // Internet Access adapter reports PROP_VIRTUAL rather than TUNNEL, so excluding only
        // tunnels would still double count everything sent over that VPN.
        Assert.False(NetworkDeviceParser.CountsTowardAggregate(ifType, isActive: true));

    [Fact]
    public void Should_Keep_An_Inactive_Adapter_Out_Of_The_Aggregate() =>
        Assert.False(NetworkDeviceParser.CountsTowardAggregate(
            NetIoApi.IF_TYPE_ETHERNET_CSMACD, isActive: false));

    [Fact]
    public void Should_Report_An_Unknown_Link_Speed_As_Zero() =>
        Assert.Equal(0UL, NetworkDeviceParser.DecodeLinkSpeed(ulong.MaxValue));

    [Fact]
    public void Should_Pass_Through_A_Known_Link_Speed() =>
        Assert.Equal(1_000_000_000UL, NetworkDeviceParser.DecodeLinkSpeed(1_000_000_000UL));

    [Fact]
    public void Should_Format_A_Mac_Address()
    {
        byte[] physicalAddress = [0x10, 0x7C, 0x61, 0x46, 0x8D, 0x3E];

        Assert.Equal("10-7C-61-46-8D-3E", NetworkDeviceParser.FormatMacAddress(physicalAddress));
    }

    [Fact]
    public void Should_Format_An_Empty_Mac_Address_As_Empty() =>
        // The loopback pseudo interface reports a zero length physical address.
        Assert.Equal(string.Empty, NetworkDeviceParser.FormatMacAddress(ReadOnlySpan<byte>.Empty));

    [Fact]
    public void Should_Format_An_IPv4_Address()
    {
        byte[] addressBytes = [192, 168, 1, 178];

        Assert.Equal("192.168.1.178", NetworkDeviceParser.FormatIPv4Address(addressBytes));
    }

    [Fact]
    public void Should_Reject_An_IPv4_Address_Of_The_Wrong_Length() =>
        Assert.Equal(string.Empty, NetworkDeviceParser.FormatIPv4Address([192, 168, 1]));

    [Fact]
    public void Should_Format_A_Link_Local_IPv6_Address_With_Its_Scope()
    {
        byte[] addressBytes = [
            0xFE, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x79, 0x5F, 0xC5, 0x78, 0xA1, 0xA5, 0x1C, 0x0F];

        Assert.Equal(
            "fe80::795f:c578:a1a5:1c0f%5",
            NetworkDeviceParser.FormatIPv6Address(addressBytes, scopeId: 5));
    }

    [Fact]
    public void Should_Compress_An_IPv6_Address()
    {
        byte[] addressBytes = new byte[16];
        addressBytes[15] = 1;

        Assert.Equal("::1", NetworkDeviceParser.FormatIPv6Address(addressBytes, scopeId: 0));
    }

    [Fact]
    public void Should_Reject_An_IPv6_Address_Of_The_Wrong_Length() =>
        Assert.Equal(string.Empty, NetworkDeviceParser.FormatIPv6Address(new byte[8], scopeId: 0));
}
