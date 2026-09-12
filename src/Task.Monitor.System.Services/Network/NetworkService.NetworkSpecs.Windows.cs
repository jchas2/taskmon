using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Network;

public partial class NetworkService
{
#if __WIN32__
    // GetAdaptersAddresses recommends starting at 15KB to avoid the resize round trip.
    private const uint InitialAdapterBufferSize = 16 * 1024;
    private const int MaxAdapterBufferAttempts = 4;

    private const uint AdapterFlags =
        IpHlpApi.GAA_FLAG_SKIP_ANYCAST |
        IpHlpApi.GAA_FLAG_SKIP_MULTICAST |
        IpHlpApi.GAA_FLAG_SKIP_DNS_SERVER;

    private NetworkSpecs? OnStartNetworkSpecs()
    {
        List<NetworkDevice>? devices = EnumerateAdapters();

        if (devices == null) {
            return null;
        }

        ApplyPhysicalMediums(devices);

        NetworkSpecs specs = new();
        specs.Devices = devices;

        return specs;
    }

    private static void ApplyPhysicalMediums(List<NetworkDevice> devices)
    {
        Dictionary<ulong, uint> mediums = new();

        if (!TryReadPhysicalMediums(mediums)) {
            return;
        }

        foreach (NetworkDevice device in devices) {
            if (mediums.TryGetValue(device.InterfaceLuid, out uint physicalMediumType)) {
                device.PhysicalMedium = NetworkDeviceParser.DecodePhysicalMedium(physicalMediumType);
            }
        }
    }

    private static unsafe List<NetworkDevice>? EnumerateAdapters()
    {
        uint size = InitialAdapterBufferSize;
        nint buffer = nint.Zero;

        try {
            for (int attempt = 0; attempt < MaxAdapterBufferAttempts; attempt++) {
                buffer = Marshal.AllocHGlobal((int)size);

                uint result = IpHlpApi.GetAdaptersAddresses(
                    WS2Def.AF_UNSPEC,
                    AdapterFlags,
                    nint.Zero,
                    (void*)buffer,
                    &size);

                if (result == IpHlpApi.ERROR_SUCCESS) {
                    return ReadAdapters((byte*)buffer);
                }

                Marshal.FreeHGlobal(buffer);
                buffer = nint.Zero;

                // No adapters at all is a valid answer, not a failure.
                if (result == IpHlpApi.ERROR_NO_DATA) {
                    return new List<NetworkDevice>();
                }

                if (result != IpHlpApi.ERROR_BUFFER_OVERFLOW) {
                    PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                        nameof(IpHlpApi.GetAdaptersAddresses),
                        $"Failed {nameof(EnumerateAdapters)}",
                        result);

                    return null;
                }

                // size now holds the length the call actually needs.
            }

            TraceEx.WriteLineOnce(
                nameof(EnumerateAdapters),
                $"{nameof(IpHlpApi.GetAdaptersAddresses)} kept asking for a larger buffer");

            return null;
        }
        finally {
            if (buffer != nint.Zero) {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static unsafe List<NetworkDevice> ReadAdapters(byte* buffer)
    {
        List<NetworkDevice> devices = new();

        for (byte* adapter = buffer; adapter != null; adapter = ReadPointer(adapter, IpTypes.AdapterAddressesNextOffset)) {
            NetworkDevice device = new();

            device.InterfaceIndex = ReadUInt32(adapter, IpTypes.AdapterAddressesIfIndexOffset);
            device.InterfaceLuid = ReadUInt64(adapter, IpTypes.AdapterAddressesLuidOffset);
            device.Name = Marshal.PtrToStringAnsi(
                (nint)ReadPointer(adapter, IpTypes.AdapterAddressesAdapterNameOffset)) ?? string.Empty;
            device.FriendlyName = Marshal.PtrToStringUni(
                (nint)ReadPointer(adapter, IpTypes.AdapterAddressesFriendlyNameOffset)) ?? string.Empty;
            device.Description = Marshal.PtrToStringUni(
                (nint)ReadPointer(adapter, IpTypes.AdapterAddressesDescriptionOffset)) ?? string.Empty;

            uint ifType = ReadUInt32(adapter, IpTypes.AdapterAddressesIfTypeOffset);
            uint operStatus = ReadUInt32(adapter, IpTypes.AdapterAddressesOperStatusOffset);

            device.ConnectionType = NetworkDeviceParser.DecodeConnectionType(ifType);
            device.OperationalStatus = NetworkDeviceParser.DecodeOperationalStatus(operStatus);

            device.TransmitLinkSpeed = NetworkDeviceParser.DecodeLinkSpeed(
                ReadUInt64(adapter, IpTypes.AdapterAddressesTransmitLinkSpeedOffset));
            device.ReceiveLinkSpeed = NetworkDeviceParser.DecodeLinkSpeed(
                ReadUInt64(adapter, IpTypes.AdapterAddressesReceiveLinkSpeedOffset));

            uint physicalAddressLength = ReadUInt32(adapter, IpTypes.AdapterAddressesPhysicalAddressLenOffset);
            int macLength = (int)Math.Min(physicalAddressLength, (uint)IpTypes.MaxAdapterAddressLength);

            device.MacAddress = NetworkDeviceParser.FormatMacAddress(
                new ReadOnlySpan<byte>(adapter + IpTypes.AdapterAddressesPhysicalAddressOffset, macLength));

            ReadUnicastAddresses(
                ReadPointer(adapter, IpTypes.AdapterAddressesFirstUnicastAddressOffset),
                out string[] ipv4Addresses,
                out string[] ipv6Addresses);

            device.IPv4Addresses = ipv4Addresses;
            device.IPv6Addresses = ipv6Addresses;

            device.IsActive = NetworkDeviceParser.IsActiveAdapter(
                ifType,
                operStatus,
                ipv4Addresses.Length + ipv6Addresses.Length);

            device.CountsTowardAggregate = NetworkDeviceParser.CountsTowardAggregate(ifType, device.IsActive);

            devices.Add(device);
        }

        return devices;
    }

    private static unsafe void ReadUnicastAddresses(
        byte* firstUnicastAddress,
        out string[] ipv4Addresses,
        out string[] ipv6Addresses)
    {
        List<string> ipv4 = new();
        List<string> ipv6 = new();

        for (byte* unicast = firstUnicastAddress;
             unicast != null;
             unicast = ReadPointer(unicast, IpTypes.UnicastAddressNextOffset)) {

            byte* socketAddress = unicast + IpTypes.UnicastAddressAddressOffset;
            byte* sockAddr = ReadPointer(socketAddress, WS2Def.SocketAddressSockAddrOffset);
            int sockAddrLength = (int)ReadUInt32(socketAddress, WS2Def.SocketAddressLengthOffset);

            if (sockAddr == null || sockAddrLength <= 0) {
                continue;
            }

            ushort family = *(ushort*)(sockAddr + WS2Def.SockAddrFamilyOffset);

            if (family == WS2Def.AF_INET &&
                sockAddrLength >= WS2Def.SockAddrIn4DataOffset + WS2Def.SockAddrIn4DataLength) {

                string address = NetworkDeviceParser.FormatIPv4Address(
                    new ReadOnlySpan<byte>(sockAddr + WS2Def.SockAddrIn4DataOffset, WS2Def.SockAddrIn4DataLength));

                if (address.Length > 0) {
                    ipv4.Add(address);
                }
            }
            else if (family == WS2Def.AF_INET6 &&
                     sockAddrLength >= WS2Def.SockAddrIn6ScopeOffset + sizeof(uint)) {

                string address = NetworkDeviceParser.FormatIPv6Address(
                    new ReadOnlySpan<byte>(sockAddr + WS2Def.SockAddrIn6DataOffset, WS2Def.SockAddrIn6DataLength),
                    ReadUInt32(sockAddr, WS2Def.SockAddrIn6ScopeOffset));

                if (address.Length > 0) {
                    ipv6.Add(address);
                }
            }
        }

        ipv4Addresses = ipv4.ToArray();
        ipv6Addresses = ipv6.ToArray();
    }

    private static unsafe byte* ReadPointer(byte* structure, int offset) =>
        (byte*)Unsafe.ReadUnaligned<nint>(structure + offset);

    private static unsafe uint ReadUInt32(byte* structure, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(structure + offset, sizeof(uint)));

    private static unsafe ulong ReadUInt64(byte* structure, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(structure + offset, sizeof(ulong)));
#endif
}
