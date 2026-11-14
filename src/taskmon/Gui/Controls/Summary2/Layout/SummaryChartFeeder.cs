using System.Globalization;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Services.Npu;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public static class SummaryChartFeeder
{
    public static void Feed(
        LayoutTree<SummaryPane>           tree,
        IReadOnlyDictionary<int, Control> paneControls,
        SystemSnapshot                    snapshot,
        AppConfig                         appConfig)
    {
        foreach ((int paneId, SummaryPane pane) in tree.Panes()) {
            if (!paneControls.TryGetValue(paneId, out Control? control) || control is not Chart chart) {
                continue;
            }

            switch (pane.ControlType) {
                case PaneControlType.Cpu when snapshot.Cpu is { } cpuInfo: {
                    CpuMetrics cpu  = cpuInfo.Metrics;
                    double totalCpu = cpu.CpuPercentKernelTime + cpu.CpuPercentUserTime;

                    chart.LabelSeries = appConfig.ShowMetreCpuNumerically
                        ? string.Format(CultureInfo.CurrentCulture, Strings.SummaryCpuChartLabel, totalCpu, cpu.CpuPercentKernelTime, cpu.CpuPercentUserTime)
                        : string.Empty;

                    chart.Add(totalCpu);
                    break;
                }

                case PaneControlType.Memory when snapshot.Memory is { } memoryInfo: {
                    MemoryMetrics memory = memoryInfo.Metrics;

                    chart.LabelSeries = appConfig.ShowMetreMemoryNumerically
                        ? memory.ToMemoryRatioFormattedBytes()
                        : string.Empty;

                    chart.Add(memory.ToMemoryRatio());
                    break;
                }

                case PaneControlType.VirtualMemory when snapshot.Memory is { } memoryInfo: {
                    MemoryMetrics memory = memoryInfo.Metrics;

                    chart.LabelSeries = appConfig.ShowMetreSwapNumerically
                        ? memory.ToPageFileMemoryRatioFormattedBytes()
                        : string.Empty;

                    chart.Add(memory.ToPageFileMemoryRatio());
                    break;
                }

                case PaneControlType.Gpu when snapshot.Gpu is { } gpuInfo: {
                    GpuMetrics gpu = gpuInfo.Metrics;

                    chart.LabelSeries = appConfig.ShowMetreGpuNumerically
                        ? gpu.ToGpuPercentage()
                        : string.Empty;

                    chart.Add(gpu.GpuPercentTime);
                    break;
                }

                case PaneControlType.GpuMemory when snapshot.Gpu is { } gpuInfo: {
                    GpuMetrics gpu = gpuInfo.Metrics;

                    chart.LabelSeries = appConfig.ShowMetreGpuMemNumerically
                        ? gpu.ToGpuMemoryRatioFormattedBytes()
                        : string.Empty;

                    chart.Add(gpu.ToGpuMemoryRatio());
                    break;
                }

                case PaneControlType.Npu when snapshot.Npu?.Metrics.Devices.FirstOrDefault() is { } npu: {
                    chart.LabelSeries = appConfig.ShowMetreNpuNumerically
                        ? ToNpuLabel(npu, snapshot.Power?.Metrics.NeuralEnginePower())
                        : string.Empty;

                    chart.Add(npu.ActiveRatio);
                    break;
                }

                case PaneControlType.Disk when snapshot.Disk is { } disk: {
                    chart.LabelSeries = appConfig.ShowMetreDiskNumerically
                        ? disk.Metrics.ToDiskTransferRate()
                        : string.Empty;

                    chart.Add(disk.Metrics.ToDiskTransferBytesPerSecond());
                    break;
                }

                case PaneControlType.NetworkReceived when snapshot.Network is { } networkInfo: {
                    NetworkMetrics network = networkInfo.Metrics;

                    chart.LabelSeries = appConfig.ShowMetreNetworkNumerically
                        ? network.ToNetworkReceiveRate()
                        : string.Empty;

                    chart.Add(network.ReceiveBytesPerSecond);
                    break;
                }

                case PaneControlType.NetworkSent when snapshot.Network is { } networkInfo: {
                    NetworkMetrics network = networkInfo.Metrics;

                    chart.LabelSeries = appConfig.ShowMetreNetworkNumerically
                        ? network.ToNetworkSendRate()
                        : string.Empty;

                    chart.Add(network.SendBytesPerSecond);
                    break;
                }
            }
        }
    }

    private static string ToNpuLabel(NpuDeviceMetrics npu, double? watts) =>
        watts is { } value
            ? $"{npu.ToNpuPercentage()} · {value.ToWattText()}"
            : npu.ToNpuPercentage();
}
