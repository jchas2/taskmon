#if __APPLE__
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Network;

public partial class NetworkService
{
    private static NetworkDevice? BuildNetworkDevice(
        NetworkInterface                    nic,
        IReadOnlyDictionary<string, string> displayNames,
        string?                             primaryInterface)
    {
        uint index = GetInterfaceIndex(nic);

        if (index == 0) {
            return null;
        }

        NetworkInterfaceType type = nic.NetworkInterfaceType;
        bool isUp = nic.OperationalStatus == OperationalStatus.Up;

        bool isPhysicalType =
            type is NetworkInterfaceType.Ethernet
                 or NetworkInterfaceType.GigabitEthernet
                 or NetworkInterfaceType.FastEthernetT
                 or NetworkInterfaceType.FastEthernetFx
                 or NetworkInterfaceType.Wireless80211;

        bool isPeerLink = nic.Name.StartsWith("awdl", StringComparison.Ordinal) ||
                          nic.Name.StartsWith("llw", StringComparison.Ordinal);

        List<string>    ipv4      = new();
        List<string>    ipv6      = new();
        List<IPAddress> addresses = new();

        foreach (UnicastIPAddressInformation addr in nic.GetIPProperties().UnicastAddresses) {
            addresses.Add(addr.Address);

            if (addr.Address.AddressFamily == AddressFamily.InterNetwork) {
                ipv4.Add(addr.Address.ToString());
            }
            else if (addr.Address.AddressFamily == AddressFamily.InterNetworkV6) {
                ipv6.Add(addr.Address.ToString());
            }
        }

        string connectionType = DecodeConnectionType(type);

        bool isActive = IsVisibleAdapter(
            isUp,
            type == NetworkInterfaceType.Loopback,
            isPeerLink,
            isPhysicalType,
            addresses);

        NetworkDevice device = new();
        // Not inline declared to assist with debugging.
        device.InterfaceIndex        = index;
        device.InterfaceLuid         = index; // macOS keys on the BSD index; no LUID exists.
        device.Name                  = nic.Name;
        device.FriendlyName          = displayNames.GetValueOrDefault(nic.Name, nic.Name);
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
        device.IsPrimary             = nic.Name == primaryInterface;

        return device;
    }

    // Primary adapter first, then by interface index.
    internal static int CompareAdapters(NetworkDevice left, NetworkDevice right) =>
        left.IsPrimary != right.IsPrimary
            ? left.IsPrimary ? -1 : 1
            : left.InterfaceIndex.CompareTo(right.InterfaceIndex);

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

    internal static bool IsVisibleAdapter(
        bool                     isUp,
        bool                     isLoopback,
        bool                     isPeerLink,
        bool                     isPhysicalType,
        IReadOnlyList<IPAddress> addresses) =>
        isUp &&
        !isLoopback &&
        !isPeerLink &&
        (NetworkDeviceParser.HasRoutableAddress(addresses) || (isPhysicalType && addresses.Count > 0));

    private static ulong NormaliseSpeed(long speed) => speed > 0 ? (ulong)speed : 0;

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

        metrics.Devices.Sort((left, right) => CompareAdapters(devices[left.InterfaceIndex], devices[right.InterfaceIndex]));
        metrics.SendMegabytesPerSecond    = metrics.SendBytesPerSecond / BytesPerMegabyte;
        metrics.ReceiveMegabytesPerSecond = metrics.ReceiveBytesPerSecond / BytesPerMegabyte;
    }

    private NetworkSpecs? OnStartNetworkSpecs()
    {
        NetworkSpecs specs = new();

        Dictionary<string, string> displayNames = SystemConfiguration.GetInterfaceDisplayNames();
        string? primaryInterface                = SystemConfiguration.GetPrimaryInterface();

        try {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces()) {
                try {
                    NetworkDevice? device = BuildNetworkDevice(nic, displayNames, primaryInterface);

                    if (device != null) {
                        specs.Devices.Add(device);
                    }
                }
                // A single adapter failing to report shouldn't drop the whole list.
                catch (NetworkInformationException nie) {
                    ExceptionHelper.LogException(nie);
                }
                catch (PlatformNotSupportedException pnse) {
                    ExceptionHelper.LogException(pnse);
                }
            }
        }
        catch (NetworkInformationException nie) {
            ExceptionHelper.LogException(nie);
            return null;
        }

        specs.Devices.Sort(CompareAdapters);
        return specs;
    }

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
}
#endif
