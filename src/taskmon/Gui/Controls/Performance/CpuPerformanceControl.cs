using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Gui.Controls.Performance;

public sealed class CpuPerformanceControl : Control, IPerformanceDetail
{
    private readonly Lock @lock = new();
    private readonly List<Chart> coreCharts;
    private Chart cpuChart;
    private MetreControl cpuMetre;
    private ListView cpuMetricsListView;
    private ListView cpuSpecsListView;
    private readonly AppConfig appConfig;
    private CpuInfo? cpuInfo;
    private double? temperature;
    
    public CpuPerformanceControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;
        coreCharts = new List<Chart>(Environment.ProcessorCount);

        for (int i = 0; i < Environment.ProcessorCount; i++) {
            coreCharts.Add(new Chart(terminal));
        }

        cpuChart = new Chart(terminal);
        cpuMetre = new MetreControl(terminal);

        cpuMetricsListView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowBorder = false,
            ShowColumnHeaders = true,
            ShowCheckboxes = false,
            Visible = true,
            TabStop = false
        };

        cpuSpecsListView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowBorder = false,
            ShowColumnHeaders = false,
            ShowCheckboxes = false,
            Visible = true,
            TabStop = false
        };
    }

    private const int ChartWidth = 14;
    private const int ChartHeight = 7;

    // Fed every tick, whether or not this pane is on screen, so the core charts and the CPU chart
    // keep a gap-free history the same way the nav mini-charts do.
    public void Sample(SystemSnapshot snapshot)
    {
        lock (@lock) {
            if (snapshot.Cpu is not { } cpu || cpu.CoreMetrics.Length == 0) {
                return;
            }

            cpuInfo = cpu;
            temperature = snapshot.Thermal?.Metrics.CpuTemperature();

            int numCores = Math.Min(Environment.ProcessorCount, cpu.CoreMetrics.Length);

            for (int i = 0; i < numCores; i++) {
                coreCharts[i].AddData(cpu.CoreMetrics[i].Value);
            }

            cpuChart.AddData(cpu.Metrics.CpuTotalTime);
        }
    }

    protected override void OnDraw()
    {
        lock (@lock) {
            if (cpuInfo == null || cpuInfo.CoreMetrics.Length == 0) {
                return;
            }

            if (Width < ChartWidth + 2 || Height < ChartHeight + 2) {
                return;
            }

            Debug.Assert(Environment.ProcessorCount == cpuInfo.CoreMetrics.Length);
            int numCores = Math.Min(Environment.ProcessorCount, cpuInfo.CoreMetrics.Length);

            for (int i = 0; i < numCores; i++) {
                coreCharts[i].Text = $"CPU {cpuInfo.CoreMetrics[i].Name}";
                coreCharts[i].Draw();
            }

            cpuChart.Text = temperature is { } cpuTemp
                ? $"{cpuInfo.Metrics.ToCpuKernelUserPercentage()}   ·   {cpuTemp.ToTemperatureText()}"
                : cpuInfo.Metrics.ToCpuKernelUserPercentage();
            cpuChart.Draw();

            // Kernel and User time as measured; the unfilled remainder of the metre is the idle
            // portion, so it needs no series of its own.
            double kernelTime = cpuInfo.Metrics.CpuPercentKernelTime;
            double userTime = cpuInfo.Metrics.CpuPercentUserTime;

            cpuMetre.SetValues(kernelTime, userTime);

            cpuMetricsListView.Items[0].SubItems[0].Text = cpuInfo.Metrics.ToCpuPercentage();
            cpuMetricsListView.Items[0].SubItems[1].Text = cpuInfo.Specs.ToCpuFrequencyGhz();
            cpuMetricsListView.Items[0].SubItems[2].Text = "0";
            cpuMetricsListView.Items[0].SubItems[3].Text = "0";
            cpuMetricsListView.Draw();

            Terminal.SetCursorPosition(X + 2, cpuMetricsListView.Y + cpuMetricsListView.Height + 1);
            Terminal.Write(cpuInfo.Specs.CpuName.ToColour(appConfig.DefaultTheme.Foreground, appConfig.DefaultTheme.Background));

            cpuSpecsListView.Items[0].SubItems[1].Text = cpuInfo.Specs.ToCpuFrequencyGhz();
            cpuSpecsListView.Items[0].SubItems[3].Text = cpuInfo.Specs.ToCpuSocketCount();
            cpuSpecsListView.Items[1].SubItems[1].Text = cpuInfo.Specs.CpuCores.ToString();
            cpuSpecsListView.Items[1].SubItems[3].Text = cpuInfo.Specs.ToCpuVirtualization();
            cpuSpecsListView.Items[2].SubItems[1].Text = cpuInfo.Specs.ToCpuL1Cache();
            cpuSpecsListView.Items[2].SubItems[3].Text = cpuInfo.Specs.ToCpuL2Cache();
            cpuSpecsListView.Items[3].SubItems[1].Text = cpuInfo.Specs.ToCpuL3Cache();
            cpuSpecsListView.Draw();
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;
        
        foreach (Chart chart in coreCharts) {
            OnLoadChart(chart);
        };
        
        OnLoadChart(cpuChart);
        cpuChart.ShowGrid = true;
        cpuChart.ShowYAxisScale = true;

        cpuMetre.BackgroundColour = appConfig.DefaultTheme.Background;
        cpuMetre.ForegroundColour = appConfig.DefaultTheme.Foreground;
        cpuMetre.BorderColour = appConfig.DefaultTheme.ChartBorder;
        cpuMetre.MetreStyle = appConfig.MetreStyle;
        cpuMetre.Border = true;
        cpuMetre.Text = string.Empty;
        cpuMetre.ShowLegend = true;
        cpuMetre.Rows = 3;

        cpuMetre.AddSeries("Kernel", appConfig.DefaultTheme.RangeHighBackground);
        cpuMetre.AddSeries("User", appConfig.DefaultTheme.ColumnCommandNormalUserSpace);

        cpuMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Utilization"));
        cpuMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Speed"));
        cpuMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Processes"));
        cpuMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Threads"));

        ListViewItem cpuMetricsItem = new(new[] { "0.0%", "0 GHz", "0", "0" });
        cpuMetricsListView.Items.Add(cpuMetricsItem);
        OnLoadListView(cpuMetricsListView);
        
        cpuSpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));
        cpuSpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));
        cpuSpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));
        cpuSpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));

        cpuSpecsListView.Items.Add(new ListViewItem(new[] { "Base speed:",         "0 GHz", "Sockets:",        "0" }));
        cpuSpecsListView.Items.Add(new ListViewItem(new[] { "Logical processors:", "0",     "Virtualization:", "" }));
        cpuSpecsListView.Items.Add(new ListViewItem(new[] { "L1 cache:",           "0 KB",  "L2 cache:",       "0 KB" }));
        cpuSpecsListView.Items.Add(new ListViewItem(new[] { "L3 cache:",           "0 KB",  "",                "" }));
        OnLoadListView(cpuSpecsListView);
    }

    private void OnLoadChart(Chart chart)
    {
        chart.AutoScale = false;
        chart.BackgroundColour = appConfig.DefaultTheme.Background;
        chart.ForegroundColour = appConfig.DefaultTheme.Foreground;
        chart.CustomYAxisScaleFormatter = Chart.FormatYScalePercentage;
        chart.LabelSeries = string.Empty;
        chart.ShowYAxisScale = false;
        chart.BorderColour = appConfig.DefaultTheme.ChartBorder;
        chart.ColourHigh = appConfig.DefaultTheme.RangeHighBackground;
        chart.ColourLow = appConfig.DefaultTheme.RangeLowBackground;
        chart.ColourMid = appConfig.DefaultTheme.RangeMidBackground;
        chart.MetreStyle = appConfig.MetreStyle;
        chart.ShowGrid = false;
        chart.YAxisColour = appConfig.DefaultTheme.ChartYAxis;
    }

    private void OnLoadListView(ListView listView)
    {
        listView.BackgroundColour = appConfig.DefaultTheme.Background;
        listView.ForegroundColour = appConfig.DefaultTheme.Foreground;
        listView.HeaderBackgroundColour = appConfig.DefaultTheme.HeaderBackground;
        listView.HeaderForegroundColour = appConfig.DefaultTheme.HeaderForeground;

        foreach (ListViewColumnHeader columnHeader in listView.ColumnHeaders) {
            columnHeader.BackgroundColour = appConfig.DefaultTheme.HeaderBackground;
            columnHeader.ForegroundColour = appConfig.DefaultTheme.HeaderForeground;
        }
    }

    protected override void OnResize()
    {
        int numCols = Width / ChartWidth;
        int numRows = Height / ChartHeight;
        int rowCount = 0;
        int colCount = 0;
        int yTop = 0;
            
        for (int i = 0; i < Environment.ProcessorCount; i++) {
            if (i > 0 && i % numCols == 0) {
                rowCount++;
                if (rowCount == numRows) {
                    break;
                }
                colCount = 0;
            }

            coreCharts[i].X = X + 1 + ChartWidth * colCount;
            coreCharts[i].Y = Y + ChartHeight * rowCount;
            coreCharts[i].Width = ChartWidth;
            coreCharts[i].Height = ChartHeight;
            coreCharts[i].Resize();
            yTop = Y + ChartHeight * rowCount + ChartHeight;
            colCount++;
        }
        
        cpuMetre.Rows = 3;
        int metreHeight = cpuMetre.RequiredHeight;

        // The metrics and specs list views are a fixed height and anchored to the bottom of the
        // control; the CPU chart grows to fill whatever is left between the core grid and the metre.
        const int MetricsHeight = 2;
        const int SpecsGap = 3;
        const int SpecsHeight = 6;
        int bottomY = Y + Height - (MetricsHeight + SpecsGap + SpecsHeight);

        // Two rows are held back for the one-row gaps below the chart and below the metre.
        int height = Math.Max(0, bottomY - yTop - metreHeight - 2);
        cpuChart.X = X + 1;
        cpuChart.Y = yTop;
        cpuChart.Width = numCols * ChartWidth;
        cpuChart.Height = height;
        cpuChart.Resize();

        yTop += height + 1;

        cpuMetre.X = X + 1;
        cpuMetre.Y = yTop;
        cpuMetre.Width = numCols * ChartWidth;
        cpuMetre.Height = metreHeight;

        for (int i = 0; i < cpuMetricsListView.ColumnHeaders.Count(); i++) {
            cpuMetricsListView.ColumnHeaders[i].Width = i % 2 == 0
                ? 22
                : 16;
        }

        for (int i = 0; i < cpuSpecsListView.ColumnHeaders.Count(); i++) {
            cpuSpecsListView.ColumnHeaders[i].Width = i % 2 == 0
                ? 22
                : 16;
        }

        cpuMetricsListView.X = X + 2;
        cpuMetricsListView.Y = bottomY;
        cpuMetricsListView.Width = Width - 3;
        cpuMetricsListView.Height = MetricsHeight;

        cpuSpecsListView.X = X + 2;
        cpuSpecsListView.Y = bottomY + MetricsHeight + SpecsGap;
        cpuSpecsListView.Width = Width - 3;
        cpuSpecsListView.Height = SpecsHeight;
    }

    protected override void OnUnload()
    {
        cpuMetre.ClearSeries();

        cpuMetricsListView.ColumnHeaders.Clear();
        cpuMetricsListView.Items.Clear();
        
        cpuSpecsListView.ColumnHeaders.Clear();
        cpuSpecsListView.Items.Clear();
    }
}