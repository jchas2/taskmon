using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;

namespace Task.Monitor.Gui.Controls.Summary;

public sealed class SummaryControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;

    // The latest snapshot, kept whole. Each chart reads only the service it plots, so unpacking
    // this into per chart fields would just be a second copy of the same data.
    private SystemSnapshot? snapshot;

    private readonly ProcessControl processControl;
    private readonly Chart cpuChart;
    private readonly Chart memoryChart;
    private readonly Chart virtualMemoryChart;
    private readonly Chart diskChart;
    private readonly Chart gpuChart;
    private readonly Chart gpuMemChart;
    private readonly Chart networkRecdChart;
    private readonly Chart networkSentChart;
    private Chart[] charts;
    
    private const int MinProcessHeight = 8;
    private const int MinChartWidth = 25;
    private const int MinChartHeight = 4;

    public SummaryControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;
        
        processControl = new ProcessControl(
            serviceController,
            terminal,
            appConfig) {
            TabStop = true,
            TabIndex = 1
        };

        cpuChart = new Chart(terminal) {
            Text = "Cpu",
            AutoScale = false,
            CustomYAxisScaleFormatter = Chart.FormatYScalePercentage
        };

        memoryChart = new Chart(terminal) {
            Text = "Memory",
            AutoScale = false,
            CustomYAxisScaleFormatter = Chart.FormatYScalePercentage
        };

        virtualMemoryChart = new Chart(terminal) {
#if __WIN32__            
            Text = "Virtual",
#endif
#if __APPLE__            
            Text = "Swap",
#endif
            AutoScale = false,
            CustomYAxisScaleFormatter = Chart.FormatYScalePercentage
        };

        diskChart = new Chart(terminal) {
            Text = "Disk",
            AutoScale = true,
            CustomYAxisScaleFormatter = Chart.FormatYScaleCompact
        };

        gpuChart = new Chart(terminal) {
            Text = "Gpu",
            AutoScale = false,
            CustomYAxisScaleFormatter = Chart.FormatYScalePercentage
        };

        gpuMemChart = new Chart(terminal) {
            Text = "Gpu Memory",
            AutoScale = false,
            CustomYAxisScaleFormatter = Chart.FormatYScalePercentage
        };

        networkRecdChart = new Chart(terminal) {
            Text = "Net Rec",
            AutoScale = true,
            CustomYAxisScaleFormatter = Chart.FormatYScaleCompact
        };

        networkSentChart = new Chart(terminal) {
            Text = "Net Sent",
            AutoScale = true,
            CustomYAxisScaleFormatter = Chart.FormatYScaleCompact
        };

        charts = [cpuChart, gpuChart, diskChart, networkSentChart, memoryChart, gpuMemChart, virtualMemoryChart, networkRecdChart];

        foreach (Chart ctrl in charts) {
            Controls.Add(ctrl);
        }
        
        Controls.Add(processControl);
    }
    
    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();
            OnDrawCharts();
            OnDrawProcesses();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    // A chart is only advanced when the service that feeds it has published. Adding a zero for a
    // service that has not reported yet would put a false trough in its history that never washes
    // out, because the chart keeps every point it is given.
    private void OnDrawCharts()
    {
        if (snapshot == null) {
            return;
        }

        if (snapshot.Cpu != null) {
            CpuMetrics cpu = snapshot.Cpu.Metrics;
            double totalCpu = cpu.CpuPercentKernelTime + cpu.CpuPercentUserTime;

            cpuChart.LabelSeries = appConfig.ShowMetreCpuNumerically
                ? $"{totalCpu:000.0%} Kernel {cpu.CpuPercentKernelTime:000.0%} User {cpu.CpuPercentUserTime:000.0%}"
                : string.Empty;

            cpuChart.Add(totalCpu);
        }

        if (snapshot.Memory != null) {
            MemoryMetrics memory = snapshot.Memory.Metrics;

            memoryChart.LabelSeries = appConfig.ShowMetreMemoryNumerically
                ? memory.ToMemoryRatioFormattedBytes()
                : string.Empty;

            memoryChart.Add(memory.ToMemoryRatio());

            virtualMemoryChart.LabelSeries = appConfig.ShowMetreSwapNumerically
                ? memory.ToPageFileMemoryRatioFormattedBytes()
                : string.Empty;

            virtualMemoryChart.Add(memory.ToPageFileMemoryRatio());
        }

        if (snapshot.Gpu != null) {
            GpuMetrics gpu = snapshot.Gpu.Metrics;

            gpuChart.LabelSeries = appConfig.ShowMetreGpuNumerically
                ? gpu.ToGpuPercentage()
                : string.Empty;

            gpuChart.Add(gpu.GpuPercentTime);

            gpuMemChart.LabelSeries = appConfig.ShowMetreGpuMemNumerically
                ? gpu.ToGpuMemoryRatioFormattedBytes()
                : string.Empty;

            gpuMemChart.Add(gpu.ToGpuMemoryRatio());
        }

        if (snapshot.Disk != null) {
            // Read plus write across the physical disks, where this used to be the sum of the per
            // process byte counters. The device layer figure is the one that belongs on a chart
            // labelled Disk; the per process counters are logical i/o and read higher.
            double diskMbps = snapshot.Disk.Metrics.ToDiskTransferBytesPerSecond().ToMbpsFromBytes();

            diskChart.LabelSeries = appConfig.ShowMetreDiskNumerically
                ? $"{diskMbps} MB/s"
                : string.Empty;

            diskChart.Add(diskMbps);
        }

        if (snapshot.Network != null) {
            NetworkMetrics network = snapshot.Network.Metrics;

            networkRecdChart.LabelSeries = appConfig.ShowMetreNetworkNumerically
                ? network.ToNetworkReceiveRate()
                : string.Empty;

            networkRecdChart.Add(network.ReceiveBytesPerSecond);

            networkSentChart.LabelSeries = appConfig.ShowMetreNetworkNumerically
                ? network.ToNetworkSendRate()
                : string.Empty;

            networkSentChart.Add(network.SendBytesPerSecond);
        }
    }

    private void OnDrawProcesses()
    {
        int processCount = snapshot?.Processes?.Metrics.ProcessCount ?? 0;

        processControl.HeaderText = $"Top {appConfig.SortColumn.ToString().ToUpper()} Processes ({processControl.NumberOfProcesses})    {processCount} Total";
        processControl.FooterText = $"Pg Up | Pg Down | \u2193 Scroll Down | \u2191 Scroll Up | Sort Asc: a | Sort Desc: d";
        processControl.Draw();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        processControl.KeyPressed(keyInfo, ref handled);
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;
        
        foreach (Control ctrl in Controls) {
            ctrl.BackgroundColour = appConfig.DefaultTheme.Background;
            ctrl.ForegroundColour = appConfig.DefaultTheme.Foreground; 
        }

        foreach (Chart chart in charts) {
            chart.BorderColour = appConfig.DefaultTheme.ChartBorder;
            chart.ColourHigh = appConfig.DefaultTheme.RangeHighBackground;
            chart.ColourLow = appConfig.DefaultTheme.RangeLowBackground;
            chart.ColourMid = appConfig.DefaultTheme.RangeMidBackground;
            chart.MetreStyle = appConfig.MetreStyle;
            chart.ShowYAxisScale = appConfig.ShowYAxisScale;
            chart.YAxisColour = appConfig.DefaultTheme.ChartYAxis;
        }

        processControl.NumberOfProcesses = 20;

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        snapshot = e.Snapshot;
        Draw();
    }

    protected override void OnResize()
    {
        // int colWidth = Width / 3;
        // int row1Height = (int)((double)Height * 0.4);
        // int rowNHeight = (int)((double)Height * 0.15);
        //
        // int delta = Height - (row1Height + rowNHeight * 4);
        //
        // if (delta > 0) {
        //     row1Height += delta;
        // }
        //
        // cpuChart.X = X;
        // cpuChart.Y = Y;
        // cpuChart.Width = colWidth;
        // cpuChart.Height = row1Height;
        //
        // gpuChart.X = cpuChart.X + colWidth;
        // gpuChart.Y = Y;
        // gpuChart.Width = colWidth;
        // gpuChart.Height = row1Height;
        //
        // processControl.X = gpuChart.X + colWidth + 1;
        // processControl.Y = Y;
        // processControl.Width = colWidth - 1;
        // processControl.Height = row1Height;
        //
        // memoryChart.X = X;
        // memoryChart.Y = Y + cpuChart.Height;
        // memoryChart.Width = Width;
        // memoryChart.Height = rowNHeight;
        //
        // gpuMemChart.X = X;
        // gpuMemChart.Y = Y + cpuChart.Height + memoryChart.Height;
        // gpuMemChart.Width = Width;
        // gpuMemChart.Height = rowNHeight;
        //
        // colWidth = Width / 2;
        //
        // virtualMemoryChart.X = X;
        // virtualMemoryChart.Y = gpuMemChart.Y + gpuMemChart.Height;
        // virtualMemoryChart.Width = colWidth;
        // virtualMemoryChart.Height = rowNHeight;
        //
        // networkSentChart.X = virtualMemoryChart.X + colWidth;
        // networkSentChart.Y = virtualMemoryChart.Y;
        // networkSentChart.Width = colWidth;
        // networkSentChart.Height = rowNHeight;
        //
        // diskChart.X = X;
        // diskChart.Y = virtualMemoryChart.Y + virtualMemoryChart.Height;
        // diskChart.Width = colWidth;
        // diskChart.Height = rowNHeight;
        //
        // networkRecdChart.X = diskChart.X + colWidth;
        // networkRecdChart.Y = diskChart.Y;
        // networkRecdChart.Width = colWidth;
        // networkRecdChart.Height = rowNHeight;

        foreach (Chart ctrl in charts) {
            ctrl.Visible = false;
        }

        int height = (int)(appConfig.DefaultLayout.Ratio * Height);

        if (height < MinChartHeight + MinProcessHeight || Width < MinChartWidth) { 
            return;
        }

        int countR = appConfig.DefaultLayout.Rows;

        for (int r = appConfig.DefaultLayout.Rows; r > 0; r--) {
            if (height / r > MinChartHeight) {
                countR = r;
                break;
            }
        }
        
        int countC = appConfig.DefaultLayout.Cols;

        for (int c = appConfig.DefaultLayout.Cols; c > 0; c--) {
            if (Width / c > MinChartWidth) {
                countC = c;
                break;
            }
        }
        
        int colWidth  = Width / countC;                                                                                                                           
        int rowHeight = height / countR;
        
        for (int row = 0; row < countR; row++) {
            for (int col = 0; col < countC; col++) {
                int index = row * countC + col;                                                                                                                     
                  
                if (index >= appConfig.DefaultLayout.Charts.Count) {
                    return;
                }

                int ord = appConfig.DefaultLayout.Charts[index];
                
                charts[ord].Visible = true;                                                                                                                       
                charts[ord].X = X + colWidth * col;
                charts[ord].Y = Y + rowHeight * row;
                charts[ord].Width = colWidth - 1;
                charts[ord].Height = rowHeight * (row + 1) == height ? rowHeight - 1 : rowHeight;                                                                                                                   
            }                                                                                                                                                     
        }

        processControl.X = X;
        processControl.Y = Y + height;
        processControl.Width = Width - 2;
        processControl.Height = Height - height;

        base.OnResize();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        base.OnUnload();
    }
}
