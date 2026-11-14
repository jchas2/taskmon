using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Thermal;
using Constants = Task.Monitor.System.Services.Constants;

namespace Task.Monitor.Gui.Controls.Performance;

public sealed class DiskPerformanceControl : Control, IPerformanceDetail
{
    private const int SpecsRowCount = 10;

    private readonly Lock @lock = new();
    
    private readonly Chart    activeTimeChart;
    private readonly Chart    transferRateChart;
    private readonly ListView diskMetricsListView;
    private readonly ListView diskSpecsListView;
    private readonly AppConfig appConfig;
    
    private DiskInfo? diskInfo;
    
    private double? temperature;
    private int?    scopedDiskIndex;

    private string  powerText = Strings.CommonNotAvailable;

    public DiskPerformanceControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;

        activeTimeChart   = new Chart(terminal);
        transferRateChart = new Chart(terminal);

        diskMetricsListView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowBorder        = true,
            ShowColumnHeaders = true,
            ShowCheckboxes    = false,
            Visible           = true,
            TabStop           = false
        };
        
        diskMetricsListView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.PerformanceActiveTimeColumn))
            .Add(new ListViewColumnHeader(Strings.PerformanceReadSpeedColumn))
            .Add(new ListViewColumnHeader(Strings.PerformanceWriteSpeedColumn))
            .Add(new ListViewColumnHeader(Strings.PerformancePowerColumn));

        diskSpecsListView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowBorder        = true,
            ShowColumnHeaders = false,
            ShowCheckboxes    = false,
            Visible           = true,
            TabStop           = false
        };
        
        diskSpecsListView.ColumnHeaders
            .Add(new ListViewColumnHeader(""))
            .Add(new ListViewColumnHeader(""));
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
                ? $"{string.Format(CultureInfo.CurrentCulture, Strings.PerformanceActiveTimeChart, activeText)}   ·   {diskTemp.ToTemperatureText()}"
                : string.Format(CultureInfo.CurrentCulture, Strings.PerformanceActiveTimeChart, activeText);
            
            activeTimeChart.Draw();

            transferRateChart.Text = string.Format(CultureInfo.CurrentCulture, Strings.PerformanceDiskTransferChart, transferText);
            transferRateChart.Draw();

            diskMetricsListView.Items[0].SubItems[0].Text = activeText;
            diskMetricsListView.Items[0].SubItems[1].Text = device?.ToDiskReadRate() ?? metrics.ToDiskReadRate();
            diskMetricsListView.Items[0].SubItems[2].Text = device?.ToDiskWriteRate() ?? metrics.ToDiskWriteRate();
            diskMetricsListView.Items[0].SubItems[3].Text = powerText;
            diskMetricsListView.Draw();

            if (device != null) {
                SetSpecsRow(0, Strings.LabelModel,               deviceSpec?.Model            ?? Constants.NotAvailable);
                SetSpecsRow(1, Strings.LabelManufacturer,        deviceSpec?.Manufacturer     ?? Constants.NotAvailable);
                SetSpecsRow(2, Strings.LabelFirmwareRevision,   deviceSpec?.FirmwareRevision ?? Constants.NotAvailable);
                SetSpecsRow(3, Strings.LabelSerialNumber,       deviceSpec?.SerialNumber     ?? Constants.NotAvailable);
                SetSpecsRow(4, Strings.LabelBusType,            deviceSpec?.BusType          ?? Constants.NotAvailable);
                SetSpecsRow(5, Strings.LabelMediaType,          deviceSpec?.MediaType        ?? Constants.NotAvailable);
                SetSpecsRow(6, Strings.LabelCapacity,            (deviceSpec?.Capacity ?? 0L).ToFormattedByteSize());
                SetSpecsRow(7, Strings.LabelRemovable,           deviceSpec is { IsRemovable: true } ? Strings.CommonYes : Strings.CommonNo);
                SetSpecsRow(8, Strings.PerformanceTotalBytesRead,    device.TotalBytesRead.ToFormattedByteSize());
                SetSpecsRow(9, Strings.PerformanceTotalBytesWritten, device.TotalBytesWritten.ToFormattedByteSize());
            }
            else {
                SetSpecsRow(0, Strings.PerformanceNumberOfDisks,     diskInfo.Specs.Devices.Count.ToString());
                SetSpecsRow(1, Strings.LabelTotalCapacity,      diskInfo.Specs.ToDiskTotalCapacity().ToFormattedByteSize());
                SetSpecsRow(2, Strings.PerformanceTotalBytesRead,    metrics.TotalBytesRead.ToFormattedByteSize());
                SetSpecsRow(3, Strings.PerformanceTotalBytesWritten, metrics.TotalBytesWritten.ToFormattedByteSize());

                for (int row = 4; row < diskSpecsListView.Items.Count; row++) {
                    SetSpecsRow(row, string.Empty, string.Empty);
                }
            }

            diskSpecsListView.Draw();
        }
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(diskMetricsListView, appConfig.Theme);
        CommonControl.Theme(diskSpecsListView, appConfig.Theme);
        
        OnLoadChart(activeTimeChart, autoScale: false, Chart.FormatYScalePercentage);
        OnLoadChart(transferRateChart, autoScale: true, Chart.FormatYScaleByteRate);

        ListViewItem diskMetricsItem = new(new[] { "0.0%", "0.0 B/s", "0.0 B/s", Strings.CommonNotAvailable });
        diskMetricsListView.Items.Add(diskMetricsItem);
        
        diskSpecsListView.Items.Add(CommonControl.PropertyRow(Strings.LabelModel,               Constants.NotAvailable, appConfig.Theme));
        diskSpecsListView.Items.Add(CommonControl.PropertyRow(Strings.LabelManufacturer,        Constants.NotAvailable, appConfig.Theme));
        diskSpecsListView.Items.Add(CommonControl.PropertyRow(Strings.LabelFirmwareRevision,   Constants.NotAvailable, appConfig.Theme));
        diskSpecsListView.Items.Add(CommonControl.PropertyRow(Strings.LabelSerialNumber,       Constants.NotAvailable, appConfig.Theme));
        diskSpecsListView.Items.Add(CommonControl.PropertyRow(Strings.LabelBusType,            Constants.NotAvailable, appConfig.Theme));
        diskSpecsListView.Items.Add(CommonControl.PropertyRow(Strings.LabelMediaType,          Constants.NotAvailable, appConfig.Theme));
        diskSpecsListView.Items.Add(CommonControl.PropertyRow(Strings.LabelCapacity,            "0.0 GB", appConfig.Theme));
        diskSpecsListView.Items.Add(CommonControl.PropertyRow(Strings.LabelRemovable,           Strings.CommonNo, appConfig.Theme));
        diskSpecsListView.Items.Add(CommonControl.PropertyRow(Strings.PerformanceTotalBytesRead,    "0.0 GB", appConfig.Theme));
        diskSpecsListView.Items.Add(CommonControl.PropertyRow(Strings.PerformanceTotalBytesWritten, "0.0 GB", appConfig.Theme));
    }

    private void OnLoadChart(Chart chart, bool autoScale, Func<double, string> yAxisScaleFormatter)
    {
        CommonControl.Theme(chart, appConfig.Theme);
        
        chart.AutoScale                 = autoScale;
        chart.CustomYAxisScaleFormatter = yAxisScaleFormatter;
        chart.LabelSeries               = string.Empty;
        chart.ShowYAxisScale            = appConfig.ShowYAxisScale;
        chart.MetreStyle                = appConfig.MetreStyle;
        chart.ShowGrid                  = appConfig.ShowLargeMetreGrid;
    }

    protected override void OnResize()
    {
        const int MetricsHeight = 4;
        
        int yTop        = Y;
        int specsHeight = SpecsRowCount + 3;
        int bottomY     = Y + Height - (MetricsHeight + specsHeight);
        int chartsArea  = Math.Max(0, bottomY - yTop);
        int height      = chartsArea / 2;

        activeTimeChart.X      = X + 1;
        activeTimeChart.Y      = yTop;
        activeTimeChart.Width  = Width - 1;
        activeTimeChart.Height = height;
        activeTimeChart.Resize();

        yTop += height;

        transferRateChart.X      = X + 1;
        transferRateChart.Y      = yTop;
        transferRateChart.Width  = Width - 1;
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

        diskMetricsListView.X      = X + 1;
        diskMetricsListView.Y      = bottomY;
        diskMetricsListView.Width  = Width - 1;
        diskMetricsListView.Height = MetricsHeight;

        diskSpecsListView.X      = X + 1;
        diskSpecsListView.Y      = bottomY + MetricsHeight;
        diskSpecsListView.Width  = Width - 1;
        diskSpecsListView.Height = specsHeight;
    }

    protected override void OnUnload()
    {
        diskMetricsListView.Items.Clear();
        diskSpecsListView.Items.Clear();
    }

    private string ResolvePowerText(SystemSnapshot snapshot)
    {
        if (scopedDiskIndex is not { } index || snapshot.Power is not { } power) {
            return Strings.CommonNotAvailable;
        }

        if (power.Metrics.DiskPower(index) is not { } reading) {
            return Strings.CommonNotAvailable;
        }

        return reading.IsRated
            ? string.Format(CultureInfo.CurrentCulture, Strings.PerformanceRatedWatts, reading.Watts)
            : reading.Watts.ToWattText();
    }

    private (DiskDeviceMetrics? device, DiskDevice? spec, bool render) ResolveScope()
    {
        if (scopedDiskIndex is not { } index) {
            return (null, null, true);
        }

        DiskDeviceMetrics? device = diskInfo!.Metrics.Devices.FirstOrDefault(candidate => candidate.Index == index);
        DiskDevice?        spec   = diskInfo.Specs.Devices.FirstOrDefault(candidate => candidate.Index == index);

        return (device, spec, device != null);
    }

    public void Sample(SystemSnapshot snapshot)
    {
        lock (@lock) {
            if (snapshot.Disk == null) {
                return;
            }

            diskInfo    = snapshot.Disk;
            temperature = snapshot.Thermal?.Metrics.PrimaryTemperature(ThermalComponent.Disk, scopedDiskIndex?.ToString());
            powerText   = ResolvePowerText(snapshot);

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

    public void SetScope(int? diskIndex) => scopedDiskIndex = diskIndex;

    private void SetSpecsRow(int index, string label, string value)
    {
        diskSpecsListView.Items[index].SubItems[0].Text = label;
        diskSpecsListView.Items[index].SubItems[1].Text = value;
    }
}
