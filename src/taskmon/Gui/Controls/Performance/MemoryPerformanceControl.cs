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
    private readonly Lock @lock = new();
    private Chart memoryChart;
    private Chart pageFileChart;
    private MetreControl memoryMetre;
    private ListView memoryMetricsListView;
    private ListView memorySpecsListView;
    private readonly AppConfig appConfig;
    private MemoryInfo? memoryInfo;
    
    public MemoryPerformanceControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;

        memoryChart = new Chart(terminal);
        pageFileChart = new Chart(terminal);
        
        memoryMetre = new MetreControl(terminal);

        memoryMetricsListView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowBorder = false,
            ShowColumnHeaders = true,
            ShowCheckboxes = false,
            Visible = true,
            TabStop = false
        };

        memorySpecsListView = new ListView(terminal) {
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

    // Fed every tick, whether or not this pane is on screen, so the two charts keep a gap-free
    // history the same way the nav mini-charts do.
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

            int yTop = memoryMetricsListView.Y + memoryMetricsListView.Height + 1;
            uint stickMemory = 0;
            int slotsUsed = 0;
            ushort memorySpeed = 0;
            ushort memoryConfiguredSpeed = 0;
            string? memorySummary = null;
            string? formFactor = null;
                        
            for (int i = 0; i < memoryInfo.Specs.Devices.Count; i++) {
                MemoryDevice memoryDevice = memoryInfo.Specs.Devices[i];
                    
                if (memoryDevice.SizeInMegabytes > 0) {
                    memorySpeed = memoryDevice.Speed;
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

            if (stickMemory > 0 && memorySummary != null) {
                Terminal.SetCursorPosition(X + 2, memoryMetricsListView.Y + memoryMetricsListView.Height + 1);
                Terminal.Write($"{stickMemory.ToMemoryCapacity()} {memorySummary}".ToColour(appConfig.DefaultTheme.Foreground, appConfig.DefaultTheme.Background));
            }

            memorySpecsListView.Items[0].SubItems[1].Text = memorySpeed.ToMemorySpeed();
            memorySpecsListView.Items[1].SubItems[1].Text = memoryConfiguredSpeed.ToMemoryConfiguredSpeed();
            memorySpecsListView.Items[2].SubItems[1].Text = $"{slotsUsed} of {memoryInfo.Specs.Devices.Count}";
            memorySpecsListView.Items[3].SubItems[1].Text = formFactor ?? "Unknown";
            
            memorySpecsListView.Items[0].SubItems[3].Text = memoryInfo.Metrics.InUseBytes.ToFormattedByteSize();
            memorySpecsListView.Items[1].SubItems[3].Text = memoryInfo.Metrics.ModifiedBytes.ToFormattedByteSize();
            memorySpecsListView.Items[2].SubItems[3].Text = memoryInfo.Metrics.StandbyBytes.ToFormattedByteSize();
            memorySpecsListView.Items[3].SubItems[3].Text = memoryInfo.Metrics.FreeBytes.ToFormattedByteSize();

            memorySpecsListView.Draw();
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;
        
        OnLoadChart(memoryChart);
        OnLoadChart(pageFileChart);
        
        memoryMetre.BackgroundColour = appConfig.DefaultTheme.Background;
        memoryMetre.ForegroundColour = appConfig.DefaultTheme.Foreground;
        memoryMetre.BorderColour = appConfig.DefaultTheme.ChartBorder;
        memoryMetre.MetreStyle = appConfig.MetreStyle;
        memoryMetre.Border = true;
        memoryMetre.Text = string.Empty;
        memoryMetre.ShowLegend = true;

        memoryMetre.AddSeries("In Use", appConfig.DefaultTheme.RangeHighBackground);
        memoryMetre.AddSeries("Modified", appConfig.DefaultTheme.ColumnCommandNormalUserSpace);
        memoryMetre.AddSeries("Standby", appConfig.DefaultTheme.RangeMidBackground);
        memoryMetre.AddSeries("Free", appConfig.DefaultTheme.RangeLowBackground);

        memoryMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("In Use"));
        memoryMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Available"));
#if __WIN32__
        memoryMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Committed"));
#elif __APPLE__
        memoryMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Swap File"));
#endif
        memoryMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Available"));
        
        ListViewItem memoryMetricsItem = new(new[] { "0.0 GB", "0.0 GB", "0.0 GB", "0.0 GB" });
        memoryMetricsListView.Items.Add(memoryMetricsItem);
        OnLoadListView(memoryMetricsListView);

        memorySpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));
        memorySpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));
        memorySpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));
        memorySpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));

        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Speed:",            "0 GHz", "In use:",                "0 MB" }));
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Configured speed:", "0 GHz", "Modified:",              "0 MB" }));
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Slots used:",       "0 GHz", "Standby (cached):",      "0 MB" }));
        memorySpecsListView.Items.Add(new ListViewItem(new[] { "Form factor:",      "0 GHz", "Free:",                  "0 MB" }));
        OnLoadListView(memorySpecsListView);
    }

    private void OnLoadChart(Chart chart)
    {
        chart.AutoScale = false;
        chart.BackgroundColour = appConfig.DefaultTheme.Background;
        chart.ForegroundColour = appConfig.DefaultTheme.Foreground;
        chart.CustomYAxisScaleFormatter = Chart.FormatYScalePercentage;
        chart.LabelSeries = string.Empty;
        chart.ShowYAxisScale = true;
        chart.BorderColour = appConfig.DefaultTheme.ChartBorder;
        chart.ColourHigh = appConfig.DefaultTheme.RangeHighBackground;
        chart.ColourLow = appConfig.DefaultTheme.RangeLowBackground;
        chart.ColourMid = appConfig.DefaultTheme.RangeMidBackground;
        chart.MetreStyle = appConfig.MetreStyle;
        chart.ShowGrid = true;
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
        int yTop = Y;

        memoryMetre.Rows = 3;
        int metreHeight = memoryMetre.RequiredHeight;

        // The metrics and specs list views are a fixed height and anchored to the bottom of the
        // control; the two charts grow to fill whatever is left above the metre.
        const int MetricsHeight = 2;
        const int SpecsGap = 3;
        const int SpecsHeight = 6;
        int bottomY = Y + Height - (MetricsHeight + SpecsGap + SpecsHeight);

        // One row is held back for the gap between the metre and the bottom list stack.
        int chartsArea = Math.Max(0, bottomY - yTop - metreHeight - 1);
        int height = chartsArea / 2;

        memoryChart.X = X + 1;
        memoryChart.Y = yTop;
        memoryChart.Width = Width - 1;
        memoryChart.Height = height;
        memoryChart.Resize();

        yTop += height;

        pageFileChart.X = X + 1;
        pageFileChart.Y = yTop;
        pageFileChart.Width = Width - 1;
        pageFileChart.Height = chartsArea - height;
        pageFileChart.Resize();

        yTop += chartsArea - height;

        memoryMetre.X = X + 1;
        memoryMetre.Y = yTop;
        memoryMetre.Width = Width - 1;
        memoryMetre.Height = metreHeight;

        for (int i = 0; i < memoryMetricsListView.ColumnHeaders.Count(); i++) {
            memoryMetricsListView.ColumnHeaders[i].Width = i % 2 == 0
                ? 22
                : 16;
        }

        for (int i = 0; i < memorySpecsListView.ColumnHeaders.Count(); i++) {
            memorySpecsListView.ColumnHeaders[i].Width = i % 2 == 0
                ? 22
                : 16;
        }

        memoryMetricsListView.X = X + 2;
        memoryMetricsListView.Y = bottomY;
        memoryMetricsListView.Width = Width - 3;
        memoryMetricsListView.Height = MetricsHeight;

        memorySpecsListView.X = X + 2;
        memorySpecsListView.Y = bottomY + MetricsHeight + SpecsGap;
        memorySpecsListView.Width = Width - 3;
        memorySpecsListView.Height = SpecsHeight;
    }

    protected override void OnUnload()
    {
        memoryMetre.ClearSeries();
        
        memoryMetricsListView.ColumnHeaders.Clear();
        memoryMetricsListView.Items.Clear();

        memorySpecsListView.ColumnHeaders.Clear();
        memorySpecsListView.Items.Clear();
    }
}