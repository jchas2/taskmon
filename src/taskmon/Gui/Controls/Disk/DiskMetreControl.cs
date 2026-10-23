using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Disk;

public sealed class DiskMetreControl(
    ServiceController serviceController,
    ISystemTerminal   terminal,
    AppConfig         appConfig) : SnapshotMetreControl(serviceController, terminal, appConfig)
{
    protected override MetreReading? Read(SystemSnapshot snapshot) =>
        snapshot.Disk?.Metrics is { } disk
            ? new MetreReading(
                [disk.ToDiskActiveTimeRatio()],
                $"{FormatPercentage(disk.ToDiskActiveTimeRatio())} · {disk.ToDiskTransferRate()}")
            : null;

    protected override string Title => "Disk";
}
