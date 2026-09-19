using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

// Charts are passive - unlike Process/Drivers/Services panes, which subscribe to snapshots
// themselves, a Chart only moves when something calls Add() on it. Shared by SummaryControl2 and
// LayoutDesignerScreen so a chart pane previews identically in both.
//
// A chart is only advanced when the service that feeds it has published - adding a zero for a
// service that has not reported yet would put a false trough in its history that never washes
// out, because the chart keeps every point it is given.
public static class SummaryChartFeeder
{
    public static void Feed(
        SummaryLayoutTree tree,
        IReadOnlyDictionary<int, Control> paneControls,
        SystemSnapshot snapshot,
        AppConfig appConfig)
    {
        foreach (SummaryLayoutNode pane in tree.Panes()) {
            if (!paneControls.TryGetValue(pane.Id, out Control? control) || control is not Chart chart) {
                continue;
            }

            switch (pane.ControlType) {
                case PaneControlType.Cpu when snapshot.Cpu is { } cpuInfo: {
                    CpuMetrics cpu = cpuInfo.Metrics;
                    double totalCpu = cpu.CpuPercentKernelTime + cpu.CpuPercentUserTime;

                    chart.LabelSeries = appConfig.ShowMetreCpuNumerically
                        ? $"{totalCpu:000.0%} Kernel {cpu.CpuPercentKernelTime:000.0%} User {cpu.CpuPercentUserTime:000.0%}"
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

                case PaneControlType.Disk when snapshot.Disk is { } disk: {
                    // Read plus write across the physical disks - the device layer figure is the
                    // one that belongs on a chart labelled Disk; the per process counters are
                    // logical i/o and read higher.
                    double diskMbps = disk.Metrics.ToDiskTransferBytesPerSecond().ToMbpsFromBytes();

                    chart.LabelSeries = appConfig.ShowMetreDiskNumerically
                        ? $"{diskMbps} MB/s"
                        : string.Empty;

                    chart.Add(diskMbps);
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
}
