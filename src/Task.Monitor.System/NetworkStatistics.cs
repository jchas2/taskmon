namespace Task.Monitor.System;

public struct NetworkStatistics
{
    public ulong NetworkBytesSent { get; set; }
    public ulong NetworkBytesReceived { get; set; }
    public ulong NetworkPacketsSent { get; set; }
    public ulong NetworkPacketsReceived { get; set; }
}