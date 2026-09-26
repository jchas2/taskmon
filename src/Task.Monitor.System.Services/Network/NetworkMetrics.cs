namespace Task.Monitor.System.Services.Network;

public sealed class NetworkMetrics
{
    public ulong  TotalBytesSent              { get; set; }
    public ulong  TotalBytesReceived          { get; set; }
    public ulong  TotalPacketsSent            { get; set; }
    public ulong  TotalPacketsReceived        { get; set; }
    public double SendBytesPerSecond          { get; set; }
    public double ReceiveBytesPerSecond       { get; set; }
    public double SendPacketsPerSecond        { get; set; }
    public double ReceivePacketsPerSecond     { get; set; }
    public double SendMegabytesPerSecond      { get; set; }
    public double ReceiveMegabytesPerSecond   { get; set; }
    public List<NetworkDeviceMetrics> Devices { get; set; } = new();
}
