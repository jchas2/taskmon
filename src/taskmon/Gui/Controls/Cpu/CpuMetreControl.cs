using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Cpu;

public sealed class CpuMetreControl(
    ServiceController serviceController,
    ISystemTerminal   terminal,
    AppConfig         appConfig) : SnapshotMetreControl(serviceController, terminal, appConfig)
{
    protected override void AddSeries(MetreControl metre, Theme theme)
    {
        metre.AddSeries("Kernel", theme.PerformanceCpuKernel);
        metre.AddSeries("User",   theme.PerformanceCpuUser);
    }

    protected override MetreReading? Read(SystemSnapshot snapshot) =>
        snapshot.Cpu?.Metrics is { } cpu
            ? new MetreReading(
                [cpu.CpuPercentKernelTime, cpu.CpuPercentUserTime],
                FormatPercentage(cpu.CpuPercentKernelTime + cpu.CpuPercentUserTime))
            : null;

    protected override string Title => "CPU%";
}
