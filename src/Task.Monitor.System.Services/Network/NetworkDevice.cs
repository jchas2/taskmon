namespace Task.Monitor.System.Services.Network;

// One network adapter, as reported by GetAdaptersAddresses. InterfaceLuid is the join key to the
// MIB_IF_ROW2 counters: it is stable, whereas InterfaceIndex can be reused when adapters come and
// go.
public sealed class NetworkDevice
{
    public uint     InterfaceIndex    { get; set; }
    public ulong    InterfaceLuid     { get; set; }
    public string   Name              { get; set; } = string.Empty;
    public string   FriendlyName      { get; set; } = string.Empty;

    // The driver supplies make and model as a single string, for example "Realtek Gaming 2.5GbE
    // Family Controller #2". There is no separate vendor field the way a storage descriptor has
    // one, so this is left whole rather than split on a guess.
    public string   Description       { get; set; } = string.Empty;

    public string   ConnectionType    { get; set; } = NetworkDeviceParser.NotAvailable;
    public string   PhysicalMedium    { get; set; } = NetworkDeviceParser.NotAvailable;
    public string   MacAddress        { get; set; } = string.Empty;
    public string[] IPv4Addresses     { get; set; } = [];
    public string[] IPv6Addresses     { get; set; } = [];
    public ulong    TransmitLinkSpeed { get; set; }
    public ulong    ReceiveLinkSpeed  { get; set; }
    public string   OperationalStatus { get; set; } = NetworkDeviceParser.NotAvailable;

    // Up, not loopback, and carrying at least one address.
    public bool     IsActive          { get; set; }

    // A tunnel or dial up adapter carries the same bytes as the physical adapter underneath it,
    // so it is reported on its own but kept out of the aggregate to avoid counting twice.
    public bool     CountsTowardAggregate { get; set; }
}
