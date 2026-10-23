using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Network;

public sealed class NetworkReceiveMetreControl(
    ServiceController serviceController,
    ISystemTerminal   terminal,
    AppConfig         appConfig) : SnapshotMetreControl(serviceController, terminal, appConfig)
{
    private readonly RollingPeak peak = new(NetworkMetreScale.Window, NetworkMetreScale.FloorBytesPerSecond);

    protected override bool ColourByLevel => false;

    protected override MetreReading? Read(SystemSnapshot snapshot) =>
        snapshot.Network?.Metrics is { } network
            ? new MetreReading([peak.Add(network.ReceiveBytesPerSecond)], network.ToNetworkReceiveRate())
            : null;

    protected override string Title => "Net Receive";
}
