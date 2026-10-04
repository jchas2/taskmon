#if __APPLE__
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Task.Monitor.System.Services.Network;

public partial class NetworkService
{
    private NetworkSpecs? OnStartNetworkSpecs()
    {
        NetworkSpecs specs = new();

        try {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces()) {
                try {
                    NetworkDevice? device = BuildNetworkDevice(nic);

                    if (device != null) {
                        specs.Devices.Add(device);
                    }
                }
                catch (NetworkInformationException) {
                    // A single adapter failing to report shouldn't drop the whole list.
                }
                catch (PlatformNotSupportedException) {
                }
            }
        }
        catch (NetworkInformationException) {
            return null;
        }

        return specs;
    }

    private static NetworkDevice? BuildNetworkDevice(NetworkInterface nic)
    {
        uint index = GetInterfaceIndex(nic);

        if (index == 0) {
            return null;
        }

        NetworkInterfaceType type = nic.NetworkInterfaceType;
        bool isUp = nic.OperationalStatus == OperationalStatus.Up;
        bool isActive = isUp && type != NetworkInterfaceType.Loopback;

        // Only real wired/wireless links count toward the headline throughput. macOS reports tunnels
        // (utun*) as Unknown, and awdl0/llw0 (AirDrop/peer links) as Wi-Fi, so both a type filter and
        // a name filter are needed to avoid double-counting VPN/peer traffic against en0.
        bool isPhysicalType =
            type is NetworkInterfaceType.Ethernet
                 or NetworkInterfaceType.GigabitEthernet
                 or NetworkInterfaceType.FastEthernetT
                 or NetworkInterfaceType.FastEthernetFx
                 or NetworkInterfaceType.Wireless80211;

        bool isPeerLink = nic.Name.StartsWith("awdl", StringComparison.Ordinal) ||
                          nic.Name.StartsWith("llw", StringComparison.Ordinal);

        List<string> ipv4 = new();
        List<string> ipv6 = new();

        foreach (UnicastIPAddressInformation addr in nic.GetIPProperties().UnicastAddresses) {
            if (addr.Address.AddressFamily == AddressFamily.InterNetwork) {
                ipv4.Add(addr.Address.ToString());
            }
            else if (addr.Address.AddressFamily == AddressFamily.InterNetworkV6) {
                ipv6.Add(addr.Address.ToString());
            }
        }

        string connectionType = DecodeConnectionType(type);

        NetworkDevice device = new();
        // Not inline declared to assist with debugging.
        device.InterfaceIndex        = index;
        device.InterfaceLuid         = index; // macOS keys on the BSD index; no LUID exists.
        device.Name                  = nic.Name;
        device.FriendlyName          = nic.Name;
        device.Description           = nic.Description;
        device.ConnectionType        = connectionType;
        device.PhysicalMedium        = connectionType;
        device.MacAddress            = NetworkDeviceParser.FormatMacAddress(nic.GetPhysicalAddress().GetAddressBytes());
        device.IPv4Addresses         = ipv4.ToArray();
        device.IPv6Addresses         = ipv6.ToArray();
        device.TransmitLinkSpeed     = NormaliseSpeed(nic.Speed);
        device.ReceiveLinkSpeed      = device.TransmitLinkSpeed;
        device.OperationalStatus     = nic.OperationalStatus.ToString();
        device.IsActive              = isActive;
        device.CountsTowardAggregate = isActive && isPhysicalType && !isPeerLink;

        return device;
    }

    private static uint GetInterfaceIndex(NetworkInterface nic)
    {
        try {
            IPInterfaceProperties props = nic.GetIPProperties();

            if (nic.Supports(NetworkInterfaceComponent.IPv4)) {
                return (uint)props.GetIPv4Properties().Index;
            }

            if (nic.Supports(NetworkInterfaceComponent.IPv6)) {
                return (uint)props.GetIPv6Properties().Index;
            }
        }
        catch (NetworkInformationException) {
        }

        return 0;
    }

    private static ulong NormaliseSpeed(long speed) => speed > 0 ? (ulong)speed : 0;

    private static string DecodeConnectionType(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Ethernet         => "Ethernet",
        NetworkInterfaceType.GigabitEthernet  => "Ethernet",
        NetworkInterfaceType.FastEthernetT    => "Ethernet",
        NetworkInterfaceType.FastEthernetFx   => "Ethernet",
        NetworkInterfaceType.Wireless80211    => "Wi-Fi",
        NetworkInterfaceType.Loopback         => "Loopback",
        NetworkInterfaceType.Tunnel           => "Tunnel",
        NetworkInterfaceType.Ppp              => "PPP",
        _                                     => NetworkDeviceParser.NotAvailable,
    };

    private bool ShouldRefreshNetworkSpecs(NetworkSpecs specs)
    {
        if (sawUnknownAdapter) {
            sawUnknownAdapter = false;
            cyclesSinceSpecsRefresh = 0;
            return true;
        }

        if (++cyclesSinceSpecsRefresh < SpecsRefreshCycles) {
            return false;
        }

        cyclesSinceSpecsRefresh = 0;
        return true;
    }

    private void OnBuildNetworkMetrics(NetworkMetrics metrics, NetworkSpecs specs)
    {
        Dictionary<uint, NetworkDevice> devices = new();

        foreach (NetworkDevice device in specs.Devices) {
            devices[device.InterfaceIndex] = device;
        }

        foreach ((uint index, NetworkInstanceState state) in instanceStates) {
            if (!devices.TryGetValue(index, out NetworkDevice? device) || !device.IsActive) {
                continue;
            }

            NetworkDeviceMetrics deviceMetrics = new();
            // Not inline declared to assist with debugging.
            deviceMetrics.InterfaceIndex            = device.InterfaceIndex;
            deviceMetrics.InterfaceLuid             = device.InterfaceLuid;
            deviceMetrics.FriendlyName              = device.FriendlyName;
            deviceMetrics.TotalBytesSent            = state.TotalBytesSent;
            deviceMetrics.TotalBytesReceived        = state.TotalBytesReceived;
            deviceMetrics.TotalPacketsSent          = state.TotalPacketsSent;
            deviceMetrics.TotalPacketsReceived      = state.TotalPacketsReceived;
            deviceMetrics.SendBytesPerSecond        = state.SendBytesPerSecond;
            deviceMetrics.ReceiveBytesPerSecond     = state.ReceiveBytesPerSecond;
            deviceMetrics.SendPacketsPerSecond      = state.SendPacketsPerSecond;
            deviceMetrics.ReceivePacketsPerSecond   = state.ReceivePacketsPerSecond;
            deviceMetrics.SendMegabytesPerSecond    = state.SendBytesPerSecond / BytesPerMegabyte;
            deviceMetrics.ReceiveMegabytesPerSecond = state.ReceiveBytesPerSecond / BytesPerMegabyte;

            metrics.Devices.Add(deviceMetrics);

            if (!device.CountsTowardAggregate) {
                continue;
            }

            metrics.TotalBytesSent          += state.TotalBytesSent;
            metrics.TotalBytesReceived      += state.TotalBytesReceived;
            metrics.TotalPacketsSent        += state.TotalPacketsSent;
            metrics.TotalPacketsReceived    += state.TotalPacketsReceived;
            metrics.SendBytesPerSecond      += state.SendBytesPerSecond;
            metrics.ReceiveBytesPerSecond   += state.ReceiveBytesPerSecond;
            metrics.SendPacketsPerSecond    += state.SendPacketsPerSecond;
            metrics.ReceivePacketsPerSecond += state.ReceivePacketsPerSecond;
        }

        metrics.Devices.Sort((left, right) => left.InterfaceIndex.CompareTo(right.InterfaceIndex));
        metrics.SendMegabytesPerSecond    = metrics.SendBytesPerSecond / BytesPerMegabyte;
        metrics.ReceiveMegabytesPerSecond = metrics.ReceiveBytesPerSecond / BytesPerMegabyte;
    }
}
#endif
