using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Gpu;

public sealed class GpuMemoryMetreControl(
    ServiceController serviceController,
    ISystemTerminal   terminal,
    AppConfig         appConfig) : SnapshotMetreControl(serviceController, terminal, appConfig)
{
    protected override MetreReading? Read(SystemSnapshot snapshot) =>
        snapshot.Gpu?.Metrics is { } gpu
            ? new MetreReading([gpu.ToGpuMemoryRatio()], gpu.ToGpuMemoryRatioFormattedBytes())
            : null;

    protected override string Title => "GPU Memory";
}
