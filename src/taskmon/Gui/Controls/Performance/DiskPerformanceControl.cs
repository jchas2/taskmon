using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Gui.Controls.Performance;

public sealed class DiskPerformanceControl : Control, IPerformanceDetail
{
    private const int SpecsRowCount = 10;

    private readonly Lock @lock = new();
    private readonly Chart activeTimeChart;
    private readonly Chart transferRateChart;
    private readonly ListView diskMetricsListView;
    private readonly ListView diskSpecsListView;
    private readonly AppConfig appConfig;
    private DiskInfo? diskInfo;
    private double? temperature;
    private string powerText = "N/A";
    private int? scopedDiskIndex;

    public void SetScope(int? diskIndex) => scopedDiskIndex = diskIndex;

    public DiskPerformanceControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;

        activeTimeChart = new Chart(terminal);
        transferRateChart = new Chart(terminal);

        diskMetricsListView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowBorder = true,
            ShowColumnHeaders = true,
            ShowCheckboxes = false,
            Visible = true,
            TabStop = false
        };

        diskSpecsListView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowBorder = true,
            ShowColumnHeaders = false,
            ShowCheckboxes = false,
            Visible = true,
            TabStop = false
        };
    }

    public void Sample(SystemSnapshot snapshot)
    {
        lock (@lock) {
            if (snapshot.Disk == null) {
                return;
            }

            diskInfo = snapshot.Disk;
            
            temperature = snapshot.Thermal?.Metrics.PrimaryTemperature(
                ThermalComponent.Disk, scopedDiskIndex?.ToString());
            
            powerText = ResolvePowerText(snapshot);

            (DiskDeviceMetrics? device, _, bool render) = ResolveScope();

            if (!render) {
                return;
            }

            activeTimeChart.AddData(
                device?.ToDiskActiveTimeRatio() ?? diskInfo.Metrics.ToDiskActiveTimeRatio());
            
            transferRateChart.AddData(
                device?.ToDiskTransferBytesPerSecond() ?? diskInfo.Metrics.ToDiskTransferBytesPerSecond());
        }
    }

    private string ResolvePowerText(SystemSnapshot snapshot)
    {
        if (scopedDiskIndex is not { } index || snapshot.Power is not { } power) {
            return "N/A";
        }

        if (power.Metrics.DiskPower(index) is not { } reading) {
            return "N/A";
        }

        return reading.IsRated
            ? $"{reading.Watts:0.0} W (rated)"
            : reading.Watts.ToWattText();
    }

    private (DiskDeviceMetrics? device, DiskDevice? spec, bool render) ResolveScope()
    {
        if (scopedDiskIndex is not { } index) {
            return (null, null, true);
        }

        DiskDeviceMetrics? device = diskInfo!.Metrics.Devices.FirstOrDefault(candidate => candidate.Index == index);
        DiskDevice? spec = diskInfo.Specs.Devices.FirstOrDefault(candidate => candidate.Index == index);

        return (device, spec, device != null);
    }

    protected override void OnDraw()
    {
        lock (@lock) {
            if (diskInfo == null) {
                return;
            }

            DiskMetrics metrics = diskInfo.Metrics;
            (DiskDeviceMetrics? device, DiskDevice? deviceSpec, bool render) = ResolveScope();

            if (!render) {
                return;
            }

            string activeText = device?.ToDiskActiveTimePercentage() ?? metrics.ToDiskActiveTimePercentage();
            string transferText = device?.ToDiskTransferRate() ?? metrics.ToDiskTransferRate();

            activeTimeChart.Text = temperature is { } diskTemp
                ? $"Active Time {activeText}   ·   {diskTemp.ToTemperatureText()}"
                : $"Active Time {activeText}";
            
            activeTimeChart.Draw();

            transferRateChart.Text = $"Disk Transfer Rate {transferText}";
            transferRateChart.Draw();

            diskMetricsListView.Items[0].SubItems[0].Text = activeText;
            diskMetricsListView.Items[0].SubItems[1].Text = device?.ToDiskReadRate() ?? metrics.ToDiskReadRate();
            diskMetricsListView.Items[0].SubItems[2].Text = device?.ToDiskWriteRate() ?? metrics.ToDiskWriteRate();
            diskMetricsListView.Items[0].SubItems[3].Text = powerText;
            diskMetricsListView.Draw();

            if (device != null) {
                SetSpecsRow(0, "Model:",               deviceSpec?.Model            ?? DiskDeviceParser.NotAvailable);
                SetSpecsRow(1, "Manufacturer:",        deviceSpec?.Manufacturer     ?? DiskDeviceParser.NotAvailable);
                SetSpecsRow(2, "Firmware Revision:",   deviceSpec?.FirmwareRevision ?? DiskDeviceParser.NotAvailable);
                SetSpecsRow(3, "Serial Number:",       deviceSpec?.SerialNumber     ?? DiskDeviceParser.NotAvailable);
                SetSpecsRow(4, "Bus Type:",            deviceSpec?.BusType          ?? DiskDeviceParser.NotAvailable);
                SetSpecsRow(5, "Media Type:",          deviceSpec?.MediaType        ?? DiskDeviceParser.NotAvailable);
                SetSpecsRow(6, "Capacity:",            (deviceSpec?.Capacity ?? 0L).ToFormattedByteSize());
                SetSpecsRow(7, "Removable:",           deviceSpec is { IsRemovable: true } ? "Yes" : "No");
                SetSpecsRow(8, "Total Bytes Read:",    device.TotalBytesRead.ToFormattedByteSize());
                SetSpecsRow(9, "Total Bytes Written:", device.TotalBytesWritten.ToFormattedByteSize());
            }
            else {
                SetSpecsRow(0, "Number of Disks:",     diskInfo.Specs.Devices.Count.ToString());
                SetSpecsRow(1, "Total Capacity:",      diskInfo.Specs.ToDiskTotalCapacity().ToFormattedByteSize());
                SetSpecsRow(2, "Total Bytes Read:",    metrics.TotalBytesRead.ToFormattedByteSize());
                SetSpecsRow(3, "Total Bytes Written:", metrics.TotalBytesWritten.ToFormattedByteSize());

                for (int row = 4; row < diskSpecsListView.Items.Count; row++) {
                    SetSpecsRow(row, string.Empty, string.Empty);
                }
            }

            diskSpecsListView.Draw();
        }
    }

    private void SetSpecsRow(int index, string label, string value)
    {
        diskSpecsListView.Items[index].SubItems[0].Text = label;
        diskSpecsListView.Items[index].SubItems[1].Text = value;
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

        OnLoadChart(activeTimeChart, autoScale: false, Chart.FormatYScalePercentage);
        OnLoadChart(transferRateChart, autoScale: true, PerformanceChartFormatters.FormatYScaleByteRate);

        diskMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Active Time"));
        diskMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Read Speed"));
        diskMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Write Speed"));
        diskMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Power"));

        ListViewItem diskMetricsItem = new(new[] { "0.0%", "0.0 B/s", "0.0 B/s", "N/A" });
        diskMetricsListView.Items.Add(diskMetricsItem);
        OnLoadListView(diskMetricsListView);
        diskMetricsListView.BorderForegroundColour = appConfig.Theme.ChartBorderForeground;
        diskMetricsListView.BorderBackgroundColour = appConfig.Theme.ChartBorderBackground;

        diskSpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));
        diskSpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));

        diskSpecsListView.Items.Add(new ListViewItem(new[] { "Model:",               DiskDeviceParser.NotAvailable }));
        diskSpecsListView.Items.Add(new ListViewItem(new[] { "Manufacturer:",        DiskDeviceParser.NotAvailable }));
        diskSpecsListView.Items.Add(new ListViewItem(new[] { "Firmware Revision:",   DiskDeviceParser.NotAvailable }));
        diskSpecsListView.Items.Add(new ListViewItem(new[] { "Serial Number:",       DiskDeviceParser.NotAvailable }));
        diskSpecsListView.Items.Add(new ListViewItem(new[] { "Bus Type:",            DiskDeviceParser.NotAvailable }));
        diskSpecsListView.Items.Add(new ListViewItem(new[] { "Media Type:",          DiskDeviceParser.NotAvailable }));
        diskSpecsListView.Items.Add(new ListViewItem(new[] { "Capacity:",            "0.0 GB" }));
        diskSpecsListView.Items.Add(new ListViewItem(new[] { "Removable:",           "No"     }));
        diskSpecsListView.Items.Add(new ListViewItem(new[] { "Total Bytes Read:",    "0.0 GB" }));
        diskSpecsListView.Items.Add(new ListViewItem(new[] { "Total Bytes Written:", "0.0 GB" }));
        OnLoadListView(diskSpecsListView);
        diskSpecsListView.BorderForegroundColour = appConfig.Theme.ChartBorderForeground;
        diskSpecsListView.BorderBackgroundColour = appConfig.Theme.ChartBorderBackground;
    }

    private void OnLoadChart(Chart chart, bool autoScale, Func<double, string> yAxisScaleFormatter)
    {
        chart.AutoScale = autoScale;
        chart.BackgroundColour = appConfig.Theme.ChartBackground;
        chart.ForegroundColour = appConfig.Theme.Foreground;
        chart.CustomYAxisScaleFormatter = yAxisScaleFormatter;
        chart.LabelSeries = string.Empty;
        chart.ShowYAxisScale = true;
        chart.BorderForegroundColour = appConfig.Theme.ChartBorderForeground;
        chart.BorderBackgroundColour = appConfig.Theme.ChartBorderBackground;
        chart.ColourHigh = appConfig.Theme.RangeHighBackground;
        chart.ColourLow = appConfig.Theme.RangeLowBackground;
        chart.ColourMid = appConfig.Theme.RangeMidBackground;
        chart.MetreStyle = appConfig.MetreStyle;
        chart.ShowGrid = true;
        chart.YAxisColour = appConfig.Theme.ChartYAxis;
    }

    private void OnLoadListView(ListView listView)
    {
        listView.BackgroundColour = appConfig.Theme.ListViewBackground;
        listView.ForegroundColour = appConfig.Theme.ListViewForeground;
        listView.HeaderBackgroundColour = appConfig.Theme.HeaderBackground;
        listView.HeaderForegroundColour = appConfig.Theme.HeaderForeground;

        foreach (ListViewColumnHeader columnHeader in listView.ColumnHeaders) {
            columnHeader.BackgroundColour = appConfig.Theme.HeaderBackground;
            columnHeader.ForegroundColour = appConfig.Theme.HeaderForeground;
        }
    }

    protected override void OnResize()
    {
        const int MetricsHeight = 4;
        
        int yTop = Y;
        int specsHeight = SpecsRowCount + 3;
        int bottomY = Y + Height - (MetricsHeight + specsHeight);

        int chartsArea = Math.Max(0, bottomY - yTop);
        int height = chartsArea / 2;

        activeTimeChart.X = X + 1;
        activeTimeChart.Y = yTop;
        activeTimeChart.Width = Width - 1;
        activeTimeChart.Height = height;
        activeTimeChart.Resize();

        yTop += height;

        transferRateChart.X = X + 1;
        transferRateChart.Y = yTop;
        transferRateChart.Width = Width - 1;
        transferRateChart.Height = chartsArea - height;
        transferRateChart.Resize();

        diskMetricsListView.ColumnHeaders[0].Width = 15;
        diskMetricsListView.ColumnHeaders[1].Width = 16;
        diskMetricsListView.ColumnHeaders[2].Width = 16;
        diskMetricsListView.ColumnHeaders[3].Width = 16;

        for (int i = 0; i < diskSpecsListView.ColumnHeaders.Count(); i++) {
            diskSpecsListView.ColumnHeaders[i].Width = i % 2 == 0
                ? 22
                : 40;
        }

        diskMetricsListView.X = X + 1;
        diskMetricsListView.Y = bottomY;
        diskMetricsListView.Width = Width - 1;
        diskMetricsListView.Height = MetricsHeight;

        diskSpecsListView.X = X + 1;
        diskSpecsListView.Y = bottomY + MetricsHeight;
        diskSpecsListView.Width = Width - 1;
        diskSpecsListView.Height = specsHeight;
    }

    protected override void OnUnload()
    {
        diskMetricsListView.ColumnHeaders.Clear();
        diskMetricsListView.Items.Clear();

        diskSpecsListView.ColumnHeaders.Clear();
        diskSpecsListView.Items.Clear();
    }
}
