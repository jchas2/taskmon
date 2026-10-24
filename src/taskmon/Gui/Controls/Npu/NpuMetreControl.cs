using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Npu;

namespace Task.Monitor.Gui.Controls.Npu;

public sealed class NpuMetreControl(
    ServiceController serviceController,
    ISystemTerminal   terminal,
    AppConfig         appConfig) : SnapshotMetreControl(serviceController, terminal, appConfig)
{
    protected override MetreReading? Read(SystemSnapshot snapshot)
    {
        if (snapshot.Npu?.Metrics.Devices.FirstOrDefault() is not NpuDeviceMetrics device) {
            return null;
        }

        string text = snapshot.Power?.Metrics.NeuralEnginePower() is { } watts
            ? $"{FormatPercentage(device.ActiveRatio)} · {watts.ToWattText()}"
            : FormatPercentage(device.ActiveRatio);

        return new MetreReading([device.ActiveRatio], text);
    }

    protected override string Title => "NPU%";
}
