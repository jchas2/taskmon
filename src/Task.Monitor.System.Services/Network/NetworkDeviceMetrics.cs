namespace Task.Monitor.System.Services.Network;

// Throughput for a single adapter, matched to NetworkDevice.InterfaceLuid.
public sealed class NetworkDeviceMetrics
{
    public uint   InterfaceIndex             { get; set; }
    public ulong  InterfaceLuid              { get; set; }
    public string FriendlyName               { get; set; } = string.Empty;

    public ulong  TotalBytesSent             { get; set; }
    public ulong  TotalBytesReceived         { get; set; }
    public ulong  TotalPacketsSent           { get; set; }
    public ulong  TotalPacketsReceived       { get; set; }

    public double SendBytesPerSecond         { get; set; }
    public double ReceiveBytesPerSecond      { get; set; }
    public double SendPacketsPerSecond       { get; set; }
    public double ReceivePacketsPerSecond    { get; set; }
    public double SendMegabytesPerSecond     { get; set; }
    public double ReceiveMegabytesPerSecond  { get; set; }
}
