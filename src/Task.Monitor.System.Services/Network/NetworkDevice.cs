namespace Task.Monitor.System.Services.Network;

public sealed class NetworkDevice
{
    public uint     InterfaceIndex        { get; set; }
    public ulong    InterfaceLuid         { get; set; }
    public string   Name                  { get; set; } = string.Empty;
    public string   FriendlyName          { get; set; } = string.Empty;
    public string   Description           { get; set; } = string.Empty;
    public string   ConnectionType        { get; set; } = NetworkDeviceParser.NotAvailable;
    public string   PhysicalMedium        { get; set; } = NetworkDeviceParser.NotAvailable;
    public string   MacAddress            { get; set; } = string.Empty;
    public string[] IPv4Addresses         { get; set; } = [];
    public string[] IPv6Addresses         { get; set; } = [];
    public ulong    TransmitLinkSpeed     { get; set; }
    public ulong    ReceiveLinkSpeed      { get; set; }
    public string   OperationalStatus     { get; set; } = NetworkDeviceParser.NotAvailable;
    public bool     IsActive              { get; set; }
    public bool     CountsTowardAggregate { get; set; }
}
