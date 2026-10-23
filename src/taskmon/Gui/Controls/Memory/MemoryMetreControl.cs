using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Memory;

public sealed class MemoryMetreControl(
    ServiceController serviceController,
    ISystemTerminal   terminal,
    AppConfig         appConfig) : SnapshotMetreControl(serviceController, terminal, appConfig)
{
    protected override MetreReading? Read(SystemSnapshot snapshot) =>
        snapshot.Memory?.Metrics is { } memory
            ? new MetreReading([memory.ToMemoryRatio()], memory.ToMemoryRatioFormattedBytes())
            : null;

    protected override string Title => "Memory";
}
