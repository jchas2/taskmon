using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Cpu;
using Task.Monitor.Gui.Controls.Disk;
using Task.Monitor.Gui.Controls.DiskSpace;
using Task.Monitor.Gui.Controls.Drivers;
using Task.Monitor.Gui.Controls.Gpu;
using Task.Monitor.Gui.Controls.InstalledApps;
using Task.Monitor.Gui.Controls.LaunchdServices;
using Task.Monitor.Gui.Controls.Memory;
using Task.Monitor.Gui.Controls.Network;
using Task.Monitor.Gui.Controls.Npu;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Gui.Controls.Services;
using Task.Monitor.Gui.Controls.Startup;
using Task.Monitor.Gui.Controls.SystemInformation;
using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public static class SummaryPaneControlFactory
{
    public static Control Create(
        SummaryLayoutNode pane,
        ServiceController serviceController,
        ISystemTerminal   terminal,
        AppConfig         appConfig) =>
        
        pane.ControlType switch {
            PaneControlType.Cpu => new Chart(terminal) {
                Text                      = "Cpu",
                AutoScale                 = false,
                CustomYAxisScaleFormatter = Chart.FormatYScalePercentage
            },
            PaneControlType.Memory => new Chart(terminal) {
                Text                      = "Memory",
                AutoScale                 = false,
                CustomYAxisScaleFormatter = Chart.FormatYScalePercentage
            },
            PaneControlType.VirtualMemory => new Chart(terminal) {
#if __WIN32__
                Text                      = "Virtual",
#endif
#if __APPLE__
                Text                      = "Swap",
#endif
                AutoScale                 = false,
                CustomYAxisScaleFormatter = Chart.FormatYScalePercentage
            },
            PaneControlType.Disk => new Chart(terminal) {
                Text                      = "Disk",
                AutoScale                 = true,
                CustomYAxisScaleFormatter = Chart.FormatYScaleCompact
            },
            PaneControlType.Gpu => new Chart(terminal) {
                Text                      = "Gpu",
                AutoScale                 = false,
                CustomYAxisScaleFormatter = Chart.FormatYScalePercentage
            },
            PaneControlType.GpuMemory => new Chart(terminal) {
                Text                      = "Gpu Memory",
                AutoScale                 = false,
                CustomYAxisScaleFormatter = Chart.FormatYScalePercentage
            },
            PaneControlType.NetworkReceived => new Chart(terminal) {
                Text                      = "Net Rec",
                AutoScale                 = true,
                CustomYAxisScaleFormatter = Chart.FormatYScaleCompact
            },
            PaneControlType.NetworkSent => new Chart(terminal) {
                Text                      = "Net Sent",
                AutoScale                 = true,
                CustomYAxisScaleFormatter = Chart.FormatYScaleCompact
            },
#if __APPLE__
            PaneControlType.Npu => new Chart(terminal) {
                Text                      = "Npu",
                AutoScale                 = false,
                CustomYAxisScaleFormatter = Chart.FormatYScalePercentage
            },
#endif
            PaneControlType.Process => new ProcessControl(serviceController, terminal, appConfig) {
                TabStop                = true,
                VisibleColumnsOverride = pane.ProcessColumns
            },
            PaneControlType.Drivers             => new DriversControl(serviceController, terminal, appConfig)             { TabStop = true },
#if __APPLE__
            PaneControlType.Services            => new LaunchdServicesControl(serviceController, terminal, appConfig)     { TabStop = true },
#else
            PaneControlType.Services            => new ServicesControl(serviceController, terminal, appConfig)            { TabStop = true },
#endif
            PaneControlType.Startup             => new StartupControl(serviceController, terminal, appConfig)             { TabStop = true },
            PaneControlType.InstalledApps       => new InstalledAppsControl(serviceController, terminal, appConfig)       { TabStop = true },
            PaneControlType.SystemInfo          => new SystemInfoControl(serviceController, terminal, appConfig)          { TabStop = true },
            PaneControlType.DiskSpace           => new DiskSpaceControl(serviceController, terminal, appConfig)           { TabStop = true },
            PaneControlType.Thermals            => new ThermalsControl(serviceController, terminal, appConfig)            { TabStop = true, ShowPowerPane = false },
            PaneControlType.CpuCores            => new CpuCoresControl(serviceController, terminal, appConfig)            { TabStop = false },
            PaneControlType.Power               => new PowerControl(serviceController, terminal, appConfig)               { TabStop = false },
            PaneControlType.CpuMetre            => new CpuMetreControl(serviceController, terminal, appConfig)            { TabStop = false },
            PaneControlType.GpuMetre            => new GpuMetreControl(serviceController, terminal, appConfig)            { TabStop = false },
            PaneControlType.GpuMemoryMetre      => new GpuMemoryMetreControl(serviceController, terminal, appConfig)      { TabStop = false },
            PaneControlType.MemoryMetre         => new MemoryMetreControl(serviceController, terminal, appConfig)         { TabStop = false },
            PaneControlType.VirtualMemoryMetre  => new VirtualMemoryMetreControl(serviceController, terminal, appConfig)  { TabStop = false },
            PaneControlType.DiskMetre           => new DiskMetreControl(serviceController, terminal, appConfig)           { TabStop = false },
            PaneControlType.NetworkSendMetre    => new NetworkSendMetreControl(serviceController, terminal, appConfig)    { TabStop = false },
            PaneControlType.NetworkReceiveMetre => new NetworkReceiveMetreControl(serviceController, terminal, appConfig) { TabStop = false },
#if __APPLE__
            PaneControlType.Fans                => new FansControl(serviceController, terminal, appConfig)                { TabStop = false },
            PaneControlType.NpuMetre            => new NpuMetreControl(serviceController, terminal, appConfig)            { TabStop = false },
#endif
                                              _ => new EmptyPaneControl(terminal),
        };
}
