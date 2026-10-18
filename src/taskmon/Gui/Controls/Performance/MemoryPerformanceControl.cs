using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
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
            .Add(new ListViewColumnHeader("In Use"))
            .Add(new ListViewColumnHeader("Available"))
#if __WIN32__
            .Add(new ListViewColumnHeader("Committed"))
#elif __APPLE__
            .Add(new ListViewColumnHeader("Swap File"))
#endif
            .Add(new ListViewColumnHeader("Available"));
        
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

    protected override void OnDraw()
    {
        lock (@lock) {
            if (memoryInfo == null) {
                return;
            }

            if (Width < ChartWidth + 2 || Height < ChartHeight + 2) {
                return;
            }

            memoryChart.Text = $"Memory {memoryInfo.Metrics.ToMemoryPercentage()}";
            
            memoryChart.Draw();

#if __WIN32__
            pageFileChart.Text = $"Committed Memory {memoryInfo.Metrics.ToPageFileMemoryPercentage()}";
#elif __APPLE__
            pageFileChart.Text = $"Swap File {memoryInfo.Metrics.ToPageFileMemoryPercentage()}";
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
                : "Unknown";
            memorySpecsListView.Items[1].SubItems[1].Text = memorySpeed.ToMemorySpeed();
            memorySpecsListView.Items[2].SubItems[1].Text = memoryConfiguredSpeed.ToMemoryConfiguredSpeed();
            memorySpecsListView.Items[3].SubItems[1].Text = $"{slotsUsed} of {memoryInfo.Specs.Devices.Count}";
            memorySpecsListView.Items[4].SubItems[1].Text = formFactor ?? "Unknown";
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

        memoryMetre.AddSeries("In Use",   appConfig.Theme.PerformanceMemoryInUse);
        memoryMetre.AddSeries("Modified", appConfig.Theme.PerformanceMemoryModified);
        memoryMetre.AddSeries("Standby",  appConfig.Theme.PerformanceMemoryStandby);
        memoryMetre.AddSeries("Free",     appConfig.Theme.PerformanceMemoryFree);
        
        ListViewItem memoryMetricsItem = new(new[] { "0.0 GB", "0.0 GB", "0.0 GB", "0.0 GB" });
        memoryMetricsListView.Items.Add(memoryMetricsItem);
        
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Memory:",            string.Empty }));
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Speed:",             "0 GHz"      }));
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Configured speed:",  "0 GHz"      }));
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Slots used:",        "0"          }));
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Form factor:",       string.Empty }));
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "In use:",            "0 MB"       }));
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Modified:",          "0 MB"       }));
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Standby (cached):",  "0 MB"       }));
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Free:",              "0 MB"       }));
    }

    private void OnLoadChart(Chart chart)
    {
        CommonControl.Theme(chart, appConfig.Theme);
        
        chart.AutoScale                 = false;
        chart.CustomYAxisScaleFormatter = Chart.FormatYScalePercentage;
        chart.LabelSeries               = string.Empty;
        chart.ShowYAxisScale            = true;
        chart.MetreStyle                = appConfig.MetreStyle;
        chart.ShowGrid                  = true;
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
}