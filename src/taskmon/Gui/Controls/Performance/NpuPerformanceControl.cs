using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Npu;

namespace Task.Monitor.Gui.Controls.Performance;

public sealed class NpuPerformanceControl : Control, IPerformanceDetail
{
    private const int    MetricsHeight = 4;
    private const int    SpecsRowCount = 6;
    private const string NotAvailable  = "N/A";

    private readonly AppConfig appConfig;

    private readonly Lock @lock = new();

    private readonly Chart    activityChart;
    private readonly Chart    powerChart;
    private readonly ListView metricsListView;
    private readonly ListView specsListView;

    private NpuDevice?        device;
    private NpuDeviceMetrics? deviceMetrics;
    private string            chipName = NotAvailable;
    private double?           powerWatts;
    private double?           peakWatts;
    private int               scopedIndex;

    public NpuPerformanceControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;

        activityChart = new Chart(terminal);
        powerChart    = new Chart(terminal);

        metricsListView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowBorder        = true,
            ShowColumnHeaders = true,
            ShowCheckboxes    = false,
            Visible           = true,
            TabStop           = false
        };

        metricsListView.ColumnHeaders
            .Add(new ListViewColumnHeader("Utilization"))
            .Add(new ListViewColumnHeader("Power Draw"))
            .Add(new ListViewColumnHeader("Peak Power"));

        specsListView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowBorder        = true,
            ShowColumnHeaders = false,
            ShowCheckboxes    = false,
            Visible           = true,
            TabStop           = false
        };

        specsListView.ColumnHeaders
            .Add(new ListViewColumnHeader(""))
            .Add(new ListViewColumnHeader(""));
    }

    protected override void OnDraw()
    {
        lock (@lock) {
            if (device == null) {
                return;
            }

            string utilization = deviceMetrics?.ToNpuPercentage() ?? NotAvailable;

            activityChart.Text = $"{device.ToDisplayName()} {utilization}";
            activityChart.Draw();

            powerChart.Text = $"Power {powerWatts.ToWattText()}";
            powerChart.Draw();

            metricsListView.Items[0].SubItems[0].Text = utilization;
            metricsListView.Items[0].SubItems[1].Text = powerWatts.ToWattText();
            metricsListView.Items[0].SubItems[2].Text = peakWatts.ToWattText();
            metricsListView.Draw();

            specsListView.Items[0].SubItems[1].Text = device.Description;
            specsListView.Items[1].SubItems[1].Text = device.Vendor;
            specsListView.Items[2].SubItems[1].Text = chipName;
            specsListView.Items[3].SubItems[1].Text = device.Cores.ToString();
            specsListView.Items[4].SubItems[1].Text = device.Architecture is { Length: > 0 } architecture ? architecture : NotAvailable;
            specsListView.Items[5].SubItems[1].Text = device.Version is { Length: > 0 } version ? version : NotAvailable;
            specsListView.Draw();
        }
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(metricsListView, appConfig.Theme);
        CommonControl.Theme(specsListView, appConfig.Theme);

        OnLoadChart(activityChart, autoScale: false, Chart.FormatYScalePercentage);
        OnLoadChart(powerChart, autoScale: true, watts => watts.ToWattText());

        metricsListView.Items.Add(new ListViewItem(new[] { NotAvailable, NotAvailable, NotAvailable }));

        specsListView.Items.Add(new ListViewItem(new[] { "Name:",         NotAvailable }));
        specsListView.Items.Add(new ListViewItem(new[] { "Vendor:",       NotAvailable }));
        specsListView.Items.Add(new ListViewItem(new[] { "Chip:",         NotAvailable }));
        specsListView.Items.Add(new ListViewItem(new[] { "Cores:",        NotAvailable }));
        specsListView.Items.Add(new ListViewItem(new[] { "Architecture:", NotAvailable }));
        specsListView.Items.Add(new ListViewItem(new[] { "Version:",      NotAvailable }));
    }

    private void OnLoadChart(Chart chart, bool autoScale, Func<double, string> formatter)
    {
        CommonControl.Theme(chart, appConfig.Theme);

        chart.AutoScale                 = autoScale;
        chart.CustomYAxisScaleFormatter = formatter;
        chart.LabelSeries               = string.Empty;
        chart.ShowYAxisScale            = true;
        chart.MetreStyle                = appConfig.MetreStyle;
        chart.ShowGrid                  = true;
    }

    protected override void OnResize()
    {
        int specsHeight = SpecsRowCount + 3;
        int bottomY     = Y + Height - (MetricsHeight + specsHeight);
        int chartsArea  = Math.Max(0, bottomY - Y);
        int height      = chartsArea / 2;

        activityChart.X      = X + 1;
        activityChart.Y      = Y;
        activityChart.Width  = Width - 1;
        activityChart.Height = height;
        activityChart.Resize();

        powerChart.X      = X + 1;
        powerChart.Y      = Y + height;
        powerChart.Width  = Width - 1;
        powerChart.Height = chartsArea - height;
        powerChart.Resize();

        metricsListView.ColumnHeaders[0].Width = 15;
        metricsListView.ColumnHeaders[1].Width = 12;
        metricsListView.ColumnHeaders[2].Width = 12;

        specsListView.ColumnHeaders[0].Width = 18;
        specsListView.ColumnHeaders[1].Width = 50;

        metricsListView.X      = X + 1;
        metricsListView.Y      = bottomY;
        metricsListView.Width  = Width - 1;
        metricsListView.Height = MetricsHeight;

        specsListView.X      = X + 1;
        specsListView.Y      = bottomY + MetricsHeight;
        specsListView.Width  = Width - 1;
        specsListView.Height = specsHeight;
    }

    protected override void OnUnload()
    {
        metricsListView.Items.Clear();
        specsListView.Items.Clear();
    }

    internal static double? Peak(double? peak, double? watts) =>
        watts is { } value && value > (peak ?? double.MinValue) ? value : peak;

    public void Sample(SystemSnapshot snapshot)
    {
        lock (@lock) {
            if (snapshot.Npu?.Specs.Devices.FirstOrDefault(candidate => candidate.Index == scopedIndex) is not { } match) {
                return;
            }

            device        = match;
            deviceMetrics = snapshot.Npu.Metrics.Devices.FirstOrDefault(candidate => candidate.Index == scopedIndex);
            chipName      = snapshot.Cpu?.Specs.CpuName is { Length: > 0 } name ? name : NotAvailable;
            powerWatts    = snapshot.Power?.Metrics.NeuralEnginePower();
            peakWatts     = Peak(peakWatts, powerWatts);

            if (deviceMetrics != null) {
                activityChart.AddData(deviceMetrics.ActiveRatio);
            }

            if (powerWatts is { } watts) {
                powerChart.AddData(watts);
            }
        }
    }

    public void SetScope(int index) => scopedIndex = index;
}
