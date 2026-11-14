using Task.Monitor.Configuration;
using Task.Monitor.Resources;
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
        metre.AddSeries(Strings.CpuKernel, theme.PerformanceCpuKernel);
        metre.AddSeries(Strings.CpuUser,   theme.PerformanceCpuUser);
    }

    protected override MetreReading? Read(SystemSnapshot snapshot) =>
        snapshot.Cpu?.Metrics is { } cpu
            ? new MetreReading(
                [cpu.CpuPercentKernelTime, cpu.CpuPercentUserTime],
                FormatPercentage(cpu.CpuPercentKernelTime + cpu.CpuPercentUserTime))
            : null;

    protected override string Title => Strings.MetreTitleCpu;
}
