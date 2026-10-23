using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Gpu;

public sealed class GpuMetreControl(
    ServiceController serviceController,
    ISystemTerminal   terminal,
    AppConfig         appConfig) : SnapshotMetreControl(serviceController, terminal, appConfig)
{
    protected override MetreReading? Read(SystemSnapshot snapshot) =>
        snapshot.Gpu?.Metrics is { } gpu
            ? new MetreReading([gpu.GpuPercentTime], FormatPercentage(gpu.GpuPercentTime))
            : null;

    protected override string Title => "GPU%";
}
