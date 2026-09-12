namespace Task.Monitor.System.Services.Network;

public sealed class NetworkMetrics
{
    // Summed across the active adapters. Unlike disk active time, summing is the right operation
    // here because different adapters carry different traffic, but tunnel and dial up adapters
    // are excluded because they carry the same bytes as the adapter underneath them.
    //
    // Totals accumulate from per cycle deltas rather than from an absolute baseline, so an
    // adapter appearing or disappearing mid run cannot introduce a step change.
    public ulong  TotalBytesSent            { get; set; }
    public ulong  TotalBytesReceived        { get; set; }
    public ulong  TotalPacketsSent          { get; set; }
    public ulong  TotalPacketsReceived      { get; set; }

    public double SendBytesPerSecond        { get; set; }
    public double ReceiveBytesPerSecond     { get; set; }

    // Packets are a count rather than a size, so these stay a plain rate and are never run through
    // a byte formatter. Derived from the same per cycle delta the byte rates come from.
    public double SendPacketsPerSecond      { get; set; }
    public double ReceivePacketsPerSecond   { get; set; }

    // 1024 based, matching how the disk metrics and Task Manager format their rates.
    public double SendMegabytesPerSecond    { get; set; }
    public double ReceiveMegabytesPerSecond { get; set; }

    public List<NetworkDeviceMetrics> Devices { get; set; } = new();
}
