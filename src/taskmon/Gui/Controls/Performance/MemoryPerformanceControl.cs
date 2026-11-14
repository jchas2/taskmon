using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Memory;

namespace Task.Monitor.Gui.Controls.Performance;

public sealed class MemoryPerformanceControl : Control, IPerformanceDetail
{
    private const int ChartWidth  = 14;
    private const int ChartHeight = 7;

    private readonly AppConfig appConfig;

    private readonly Lock @lock = new();

    private Chart        memoryChart;
    private Chart        pageFileChart;
    private MetreControl memoryMetre;
    private ListView     memoryMetricsListView;
    private ListView     memorySpecsListView;
    
    private MemoryInfo?  memoryInfo;
    
    public MemoryPerformanceControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;

        memoryChart   = new Chart(terminal);
        pageFileChart = new Chart(terminal);
        memoryMetre   = new MetreControl(terminal);

        memoryMetricsListView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowBorder        = true,
            ShowColumnHeaders = true,
            ShowCheckboxes    = false,
            Visible           = true,
            TabStop           = false
        };

        memoryMetricsListView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.PerformanceInUseColumn))
            .Add(new ListViewColumnHeader(Strings.PerformanceAvailableColumn))
#if __WIN32__
            .Add(new ListViewColumnHeader(Strings.PerformanceCommittedColumn))
#elif __APPLE__
            .Add(new ListViewColumnHeader(Strings.PerformanceSwapFileColumn))
#endif
            .Add(new ListViewColumnHeader(Strings.PerformanceAvailableColumn));

#if __APPLE__
        memoryMetricsListView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.PerformanceSwapInColumn))
            .Add(new ListViewColumnHeader(Strings.PerformanceSwapOutColumn));
#endif
        
        memorySpecsListView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowBorder        = true,
            ShowColumnHeaders = false,
            ShowCheckboxes    = false,
            Visible           = true,
            TabStop           = false
        };
        
        memorySpecsListView.ColumnHeaders
            .Add(new ListViewColumnHeader(""))
            .Add(new ListViewColumnHeader(""));
    }

    protected override void OnDraw()
    {
        lock (@lock) {
            if (memoryInfo == null) {
                return;
            }

            if (Width < ChartWidth + 2 || Height < ChartHeight + 2) {
                return;
            }

            memoryChart.Text = string.Format(CultureInfo.CurrentCulture, Strings.PerformanceMemoryChart, memoryInfo.Metrics.ToMemoryPercentage());
            
            memoryChart.Draw();

#if __WIN32__
            pageFileChart.Text = string.Format(CultureInfo.CurrentCulture, Strings.PerformanceCommittedMemoryChart, memoryInfo.Metrics.ToPageFileMemoryPercentage());
#elif __APPLE__
            pageFileChart.Text = string.Format(CultureInfo.CurrentCulture, Strings.PerformanceSwapFileChart, memoryInfo.Metrics.ToPageFileMemoryPercentage());
#endif
            pageFileChart.Draw();

            memoryMetre.SetValues(
                memoryInfo.Metrics.InUseBytesRatio,
                memoryInfo.Metrics.ModifiedBytesRatio,
                memoryInfo.Metrics.StandbyBytesRatio,
                memoryInfo.Metrics.FreeBytesRatio);

            memoryMetricsListView.Items[0].SubItems[0].Text = memoryInfo.Metrics.ToMemoryRatioFormattedBytes();
            memoryMetricsListView.Items[0].SubItems[1].Text = memoryInfo.Metrics.ToMemoryAvailableFormattedBytes();
            memoryMetricsListView.Items[0].SubItems[2].Text = memoryInfo.Metrics.ToPageFileMemoryRatioFormattedBytes();
            memoryMetricsListView.Items[0].SubItems[3].Text = memoryInfo.Metrics.ToPageFileMemoryAvailableFormattedBytes();
#if __APPLE__
            memoryMetricsListView.Items[0].SubItems[4].Text = memoryInfo.Metrics.SwapInBytesPerSecond.ToFormattedByteRate();
            memoryMetricsListView.Items[0].SubItems[5].Text = memoryInfo.Metrics.SwapOutBytesPerSecond.ToFormattedByteRate();
#endif
            
            memoryMetricsListView.Draw();

            uint stickMemory             = 0;
            int slotsUsed                = 0;
            ushort memorySpeed           = 0;
            ushort memoryConfiguredSpeed = 0;
            
            string? memorySummary = null;
            string? formFactor    = null;
                        
            for (int i = 0; i < memoryInfo.Specs.Devices.Count; i++) {
                MemoryDevice memoryDevice = memoryInfo.Specs.Devices[i];
                    
                if (memoryDevice.SizeInMegabytes > 0) {
                    memorySpeed           = memoryDevice.Speed;
                    memoryConfiguredSpeed = memoryDevice.ConfiguredClockSpeed;
                    
                    stickMemory += memoryDevice.SizeInMegabytes;
                    slotsUsed++;
                    
                    // Show the type and brand for the first discovered stick.
                    if (string.IsNullOrEmpty(memorySummary)) {
                        memorySummary = $"{memoryDevice.MemoryType} - {memoryDevice.Manufacturer}";
                    }

                    if (string.IsNullOrEmpty(formFactor)) {
                        formFactor = memoryDevice.FormFactor;
                    }
                }
            }

            memorySpecsListView.Items[0].SubItems[1].Text = stickMemory > 0 && memorySummary != null
                ? $"{stickMemory.ToMemoryCapacity()} {memorySummary}"
                : Strings.CommonUnknown;
            memorySpecsListView.Items[1].SubItems[1].Text = memorySpeed.ToMemorySpeed();
            memorySpecsListView.Items[2].SubItems[1].Text = memoryConfiguredSpeed.ToMemoryConfiguredSpeed();
            memorySpecsListView.Items[3].SubItems[1].Text = string.Format(CultureInfo.CurrentCulture, Strings.PerformanceSlotsUsedValue, slotsUsed, memoryInfo.Specs.Devices.Count);
            memorySpecsListView.Items[4].SubItems[1].Text = formFactor ?? Strings.CommonUnknown;
            memorySpecsListView.Items[5].SubItems[1].Text = memoryInfo.Metrics.InUseBytes.ToFormattedByteSize();
            memorySpecsListView.Items[6].SubItems[1].Text = memoryInfo.Metrics.ModifiedBytes.ToFormattedByteSize();
            memorySpecsListView.Items[7].SubItems[1].Text = memoryInfo.Metrics.StandbyBytes.ToFormattedByteSize();
            memorySpecsListView.Items[8].SubItems[1].Text = memoryInfo.Metrics.FreeBytes.ToFormattedByteSize();

            memorySpecsListView.Draw();
        }
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(memoryMetricsListView, appConfig.Theme);
        CommonControl.Theme(memorySpecsListView, appConfig.Theme);
        
        OnLoadChart(memoryChart);
        OnLoadChart(pageFileChart);
        
        memoryMetre.BackgroundColour       = appConfig.Theme.MetreBackground;
        memoryMetre.ForegroundColour       = appConfig.Theme.MetreForeground;
        memoryMetre.BorderForegroundColour = appConfig.Theme.MetreBorderForeground;
        memoryMetre.BorderBackgroundColour = appConfig.Theme.MetreBorderBackground;
        memoryMetre.MetreStyle             = appConfig.MetreStyle;
        memoryMetre.Border                 = true;
        memoryMetre.Text                   = string.Empty;
        memoryMetre.ShowLegend             = true;

        memoryMetre.AddSeries(Strings.PerformanceInUseSeries,   appConfig.Theme.PerformanceMemoryInUse);
        memoryMetre.AddSeries(Strings.PerformanceModifiedSeries, appConfig.Theme.PerformanceMemoryModified);
        memoryMetre.AddSeries(Strings.PerformanceStandbySeries,  appConfig.Theme.PerformanceMemoryStandby);
        memoryMetre.AddSeries(Strings.PerformanceFreeSeries,     appConfig.Theme.PerformanceMemoryFree);
        
#if __APPLE__
        ListViewItem memoryMetricsItem = new(new[] { "0.0 GB", "0.0 GB", "0.0 GB", "0.0 GB", Strings.CommonNotAvailable, Strings.CommonNotAvailable });
#else
        ListViewItem memoryMetricsItem = new(new[] { "0.0 GB", "0.0 GB", "0.0 GB", "0.0 GB" });
#endif
        memoryMetricsListView.Items.Add(memoryMetricsItem);
        
        memorySpecsListView.Items.Add(CommonControl.PropertyRow(Strings.LabelMemory,            string.Empty, appConfig.Theme));
        memorySpecsListView.Items.Add(CommonControl.PropertyRow(Strings.LabelSpeed,             "0 GHz", appConfig.Theme));
        memorySpecsListView.Items.Add(CommonControl.PropertyRow(Strings.PerformanceConfiguredSpeed,  "0 GHz", appConfig.Theme));
        memorySpecsListView.Items.Add(CommonControl.PropertyRow(Strings.PerformanceSlotsUsed,        "0", appConfig.Theme));
        memorySpecsListView.Items.Add(CommonControl.PropertyRow(Strings.PerformanceFormFactor,       string.Empty, appConfig.Theme));
        memorySpecsListView.Items.Add(CommonControl.PropertyRow(Strings.PerformanceInUse,            "0 MB", appConfig.Theme));
        memorySpecsListView.Items.Add(CommonControl.PropertyRow(Strings.PerformanceModified,          "0 MB", appConfig.Theme));
        memorySpecsListView.Items.Add(CommonControl.PropertyRow(Strings.PerformanceStandbyCached,  "0 MB", appConfig.Theme));
        memorySpecsListView.Items.Add(CommonControl.PropertyRow(Strings.PerformanceFree,              "0 MB", appConfig.Theme));
    }

    private void OnLoadChart(Chart chart)
    {
        CommonControl.Theme(chart, appConfig.Theme);
        
        chart.AutoScale                 = false;
        chart.CustomYAxisScaleFormatter = Chart.FormatYScalePercentage;
        chart.LabelSeries               = string.Empty;
        chart.ShowYAxisScale            = appConfig.ShowYAxisScale;
        chart.MetreStyle                = appConfig.MetreStyle;
        chart.ShowGrid                  = appConfig.ShowLargeMetreGrid;
    }
    
    protected override void OnResize()
    {
        const int MetricsHeight = 4;
        const int SpecsHeight   = 12;

        memoryMetre.Rows = 3;

        int yTop        = Y;
        int metreHeight = memoryMetre.RequiredHeight;
        int bottomY     = Y + Height - (MetricsHeight + SpecsHeight);
        int chartsArea  = Math.Max(0, bottomY - yTop - metreHeight);
        int height      = chartsArea / 2;

        memoryChart.X      = X + 1;
        memoryChart.Y      = yTop;
        memoryChart.Width  = Width - 1;
        memoryChart.Height = height;
        
        memoryChart.Resize();

        yTop += height;

        pageFileChart.X      = X + 1;
        pageFileChart.Y      = yTop;
        pageFileChart.Width  = Width - 1;
        pageFileChart.Height = chartsArea - height;
        
        pageFileChart.Resize();

        yTop += chartsArea - height;

        memoryMetre.X      = X + 1;
        memoryMetre.Y      = yTop;
        memoryMetre.Width  = Width - 1;
        memoryMetre.Height = metreHeight;

        for (int i = 0; i < memoryMetricsListView.ColumnHeaders.Count(); i++) {
            memoryMetricsListView.ColumnHeaders[i].Width = i % 2 == 0
                ? 22
                : 16;
        }

        for (int i = 0; i < memorySpecsListView.ColumnHeaders.Count(); i++) {
            memorySpecsListView.ColumnHeaders[i].Width = i % 2 == 0
                ? 22
                : 40;
        }

        memoryMetricsListView.X      = X + 1;
        memoryMetricsListView.Y      = bottomY;
        memoryMetricsListView.Width  = Width - 1;
        memoryMetricsListView.Height = MetricsHeight;

        memorySpecsListView.X       = X + 1;
        memorySpecsListView.Y       = bottomY + MetricsHeight;
        memorySpecsListView.Width   = Width - 1;
        memorySpecsListView.Height  = SpecsHeight;
    }

    protected override void OnUnload()
    {
        memoryMetre.ClearSeries();
        
        memoryMetricsListView.Items.Clear();
        memorySpecsListView.Items.Clear();
    }

    public void Sample(SystemSnapshot snapshot)
    {
        lock (@lock) {
            if (snapshot.Memory is not { } memory) {
                return;
            }

            memoryInfo = memory;

            memoryChart.AddData(memory.Metrics.ToMemoryRatio());
            pageFileChart.AddData(memory.Metrics.ToPageFileMemoryRatio());
        }
    }
}
