using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Memory;

public sealed class VirtualMemoryMetreControl(
    ServiceController serviceController,
    ISystemTerminal   terminal,
    AppConfig         appConfig) : SnapshotMetreControl(serviceController, terminal, appConfig)
{
    protected override MetreReading? Read(SystemSnapshot snapshot) =>
        snapshot.Memory?.Metrics is { } memory
            ? new MetreReading([memory.ToPageFileMemoryRatio()], memory.ToPageFileMemoryRatioFormattedBytes())
            : null;

#if __APPLE__
    protected override string Title => "Swap";
#else
    protected override string Title => "Virtual";
#endif
}
