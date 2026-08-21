using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using IProcessor = Task.Monitor.Process.IProcessor;
using ProcessorEventArgs = Task.Monitor.Process.ProcessorEventArgs;

namespace Task.Monitor.Gui.Controls;

public sealed class HeaderControl : Control
{
    private readonly IProcessor processor;
    private readonly AppConfig appConfig;
    private SystemStatistics systemStatistics = new();

    private readonly Chart cpuChart;
    private readonly Chart memoryChart;
    private readonly Chart virtualMemoryChart;
    private readonly Chart diskChart;
    private readonly Chart gpuChart;
    private readonly Chart gpuMemChart;
    private readonly Chart networkRecdChart;
    private readonly Chart networkSentChart;
    private Chart[] charts;

    private const int MinHeaderRows = 3;
    private const int MinChartWidth = 25;
    private const int MinChartHeight = 4;

    public HeaderControl(
        IProcessor processor, 
        ISystemTerminal terminal,
        AppConfig appConfig) 
        : base(terminal)
    {
        this.processor = processor;
        this.appConfig = appConfig;
        
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

        // Important: Order is critical to align with .layout files.
        charts = [cpuChart, gpuChart, diskChart, networkSentChart, memoryChart, gpuMemChart, virtualMemoryChart, networkRecdChart];

        foreach (Chart ctrl in charts) {
            Controls.Add(ctrl);
        }
    }
    
    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();
            OnDrawInternal();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    private void OnDrawInternal()
    {
        const string MachineLabel = "Machine: ";
        const string OSLabel = " OS: ";
        const string IpLabel = " Ip: ";
        const string CpuLabel = "Cpu: ";
        const string TasksLabel = " Tasks: ";
        const string ThreadsLabel = " Threads: ";
        const string RunningLabel = " running";
        
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;
        
        Terminal.SetCursorPosition(X, Y);
        Terminal.BackgroundColor = appConfig.DefaultTheme.MenubarBackground;
        Terminal.ForegroundColor = appConfig.DefaultTheme.MenubarForeground;

        string menubar = "TASK MONITOR";
        int offsetX = Terminal.WindowWidth / 2 - menubar.Length / 2;
        
        Terminal.WriteEmptyLineTo(offsetX);
        Terminal.Write(menubar.ToBold());
        Terminal.WriteEmptyLineTo(Width - offsetX - menubar.Length);
        
        Terminal.BackgroundColor = BackgroundColour;
        Terminal.ForegroundColor = ForegroundColour;

        Color lbColor = appConfig.DefaultTheme.Foreground;
        Color fgColour = appConfig.DefaultTheme.RangeLowBackground;
        Color bgColour = appConfig.DefaultTheme.Background;

        Terminal.Write(MachineLabel.ToColour(lbColor, bgColour));
        Terminal.Write(systemStatistics.MachineName.ToColour(fgColour, bgColour));
        Terminal.Write(OSLabel.ToColour(lbColor, bgColour));
        Terminal.Write(systemStatistics.OsVersion.ToColour(fgColour, bgColour));
        Terminal.Write(IpLabel.ToColour(lbColor, bgColour));
        Terminal.Write(systemStatistics.PrivateIPv4Address.ToColour(fgColour, bgColour));
        
        int nchars =
            MachineLabel.Length + systemStatistics.MachineName.Length +
            OSLabel.Length + systemStatistics.OsVersion.Length +
            IpLabel.Length + systemStatistics.PrivateIPv4Address.Length;

        int themeLen = appConfig.DefaultTheme.Name.Length + 1;

        Terminal.BackgroundColor = bgColour;
        Terminal.WriteEmptyLineTo(Width - nchars - themeLen);
        Terminal.Write($"{appConfig.DefaultTheme.Name.ToColour(fgColour, bgColour)} ");

        string coreBreakdown = $"{systemStatistics.CpuCores} Cores";
#if __APPLE__
        if (systemStatistics.CpuPerformanceCores > 0) {
            coreBreakdown += $" · {systemStatistics.CpuPerformanceCores}P";
        }

        if (systemStatistics.CpuEfficiencyCores > 0) {
            coreBreakdown += $" · {systemStatistics.CpuEfficiencyCores}E";
        }

        if (systemStatistics.CpuSuperCores > 0) {
            coreBreakdown += $" · {systemStatistics.CpuSuperCores}S";
        }

        if (systemStatistics.GpuCores > 0) {
            coreBreakdown += $" · {systemStatistics.GpuCores} Gpu";
        }
#endif
        string cpuName = systemStatistics.CpuName;

        if (systemStatistics.CpuFrequency > 0) {
            cpuName += $" @ {systemStatistics.CpuFrequency / 1000.0:0.00} GHz";
        }
        
        string cpuInfo = $"{cpuName} ({coreBreakdown})";

        if (appConfig.UseIrixReporting) {
            cpuInfo += " Irix Mode";
        }
        else {
            cpuInfo += " Solaris Mode";
        }

        Terminal.Write(CpuLabel.ToColour(lbColor, bgColour));
        Terminal.Write(cpuInfo.ToColour(fgColour, bgColour));
        nchars = CpuLabel.Length + cpuInfo.Length;

        string processCount = systemStatistics.ProcessCount.ToString();
        string threadCount = systemStatistics.ThreadCount.ToString();
        string runningCount = systemStatistics.RunningCount.ToString();
        
        Terminal.Write(TasksLabel.ToColour(lbColor, bgColour));
        Terminal.Write(processCount.ToColour(fgColour, bgColour));
        Terminal.Write(ThreadsLabel.ToColour(lbColor, bgColour));
        Terminal.Write((threadCount + ", ").ToColour(fgColour, bgColour));
        Terminal.Write(runningCount.ToColour(fgColour, bgColour));
        Terminal.Write(RunningLabel.ToColour(lbColor, bgColour));

        nchars += TasksLabel.Length + processCount.Length +
                  ThreadsLabel.Length + threadCount.Length + 2 +
                  runningCount.Length + RunningLabel.Length;
        
        Terminal.BackgroundColor = bgColour;
        Terminal.WriteEmptyLineTo(Width - nchars);
        
        double totalCpu = systemStatistics.CpuPercentKernelTime + systemStatistics.CpuPercentUserTime;
        double memRatio = 0.0;
        double virRatio = 0.0;
        double diskMbps = systemStatistics.DiskUsage.ToMbpsFromBytes();
        double gpuCpu = systemStatistics.GpuPercentTime;
        double gpuMemRatio = 0.0;

        cpuChart.LabelSeries = appConfig.ShowMetreCpuNumerically
            ? $"{totalCpu:000.0%} Kernel {systemStatistics.CpuPercentKernelTime:000.0%} User {systemStatistics.CpuPercentUserTime:000.0%}"
            : string.Empty;
        
        cpuChart.Add(totalCpu);

        if (systemStatistics.TotalPhysical > 0) {
            memRatio = 1.0 - ((double)(systemStatistics.AvailablePhysical) / (double)(systemStatistics.TotalPhysical));    
        }

        memoryChart.LabelSeries = appConfig.ShowMetreMemoryNumerically
            ? (systemStatistics.TotalPhysical - systemStatistics.AvailablePhysical).ToFormattedByteSize() + "/" +
              systemStatistics.TotalPhysical.ToFormattedByteSize()
            : string.Empty;
        
        memoryChart.Add(memRatio);

        gpuChart.LabelSeries = appConfig.ShowMetreGpuNumerically
            ? gpuCpu.ToString("000.0%")
            : string.Empty;
        
        gpuChart.Add(gpuCpu);
        
        if (systemStatistics.TotalGpuMemory > 0) {
            gpuMemRatio = 1.0 - ((double)(systemStatistics.AvailableGpuMemory) / (double)(systemStatistics.TotalGpuMemory));
        }

        gpuMemChart.LabelSeries = appConfig.ShowMetreGpuMemNumerically
            ? (systemStatistics.TotalGpuMemory - systemStatistics.AvailableGpuMemory).ToFormattedByteSize() + "/" +
              systemStatistics.TotalGpuMemory.ToFormattedByteSize()
            : string.Empty;
        
        gpuMemChart.Add(gpuMemRatio);

        if (systemStatistics.TotalPageFile > 0) {
            virRatio = 1.0 - ((double)(systemStatistics.AvailablePageFile) / (double)(systemStatistics.TotalPageFile));    
        }

        virtualMemoryChart.LabelSeries = appConfig.ShowMetreSwapNumerically
            ? (systemStatistics.TotalPageFile - systemStatistics.AvailablePageFile).ToFormattedByteSize() + "/" +
              systemStatistics.TotalPageFile.ToFormattedByteSize()
            : string.Empty;
        
        virtualMemoryChart.Add(virRatio);

        diskChart.LabelSeries = appConfig.ShowMetreDiskNumerically
            ? $"{diskMbps} MB/s"
            : string.Empty;
        
        diskChart.Add(diskMbps);
        
        networkRecdChart.LabelSeries = appConfig.ShowMetreNetworkNumerically
            ? systemStatistics.NetworkBytesReceiveTime.ToFormattedByteSize()
            : string.Empty;
        
        networkRecdChart.Add(systemStatistics.NetworkBytesReceiveTime);
        
        networkSentChart.LabelSeries = appConfig.ShowMetreNetworkNumerically
            ? systemStatistics.NetworkBytesSendTime.ToFormattedByteSize()
            : string.Empty;
        
        networkSentChart.Add(systemStatistics.NetworkBytesSendTime);
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
            chart.BorderColour = appConfig.DefaultTheme.ChartBprder;
            chart.ColourHigh = appConfig.DefaultTheme.RangeHighBackground;
            chart.ColourLow = appConfig.DefaultTheme.RangeLowBackground;
            chart.ColourMid = appConfig.DefaultTheme.RangeMidBackground;
            chart.MetreStyle = appConfig.MetreStyle;
            chart.ShowYAxisScale = appConfig.ShowYAxisScale;
            chart.YAxisColour = appConfig.DefaultTheme.ChartYAxis;
        }
        
        processor.ProcessorUpdated += OnProcessorUpdated;
        
        base.OnLoad();
    }

    protected override void OnResize()
    {
        foreach (Chart ctrl in charts) {
            ctrl.Visible = false;
        }

        int height = Height - MinHeaderRows;

        if (height < MinChartHeight + MinHeaderRows || Width < MinChartWidth) { 
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
                charts[ord].X = colWidth * col;
                charts[ord].Y = rowHeight * row + MinHeaderRows;
                charts[ord].Width = colWidth - 1;
                charts[ord].Height = rowHeight * (row + 1) == height ? rowHeight - 1 : rowHeight;                                                                                                                   
            }                                                                                                                                                     
        }
        
        base.OnResize();
    }

    private void OnProcessorUpdated(object? sender, ProcessorEventArgs e)
    {
        systemStatistics = e.Statistics;
        Draw();
    }
    
    protected override void OnUnload()
    {
        processor.ProcessorUpdated -= OnProcessorUpdated;
        base.OnUnload();
    }
}