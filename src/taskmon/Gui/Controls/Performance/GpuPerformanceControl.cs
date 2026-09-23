using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Gui.Controls.Performance;

public sealed class GpuPerformanceControl : Control, IPerformanceDetail
{
    // Vendor, Description, Adapter Type, Driver Version, Driver Date. Fixed rather than taken from
    // Items.Count so a resize that lands before the load still sizes correctly.
    private const int SpecsRowCount = 5;

    private readonly Lock @lock = new();
    private Chart gpuChart;
    private Chart gpuDedicatedMemChart;
    private Chart gpuMemChart;
    private ListView gpuMetricsListView;
    private ListView gpuSpecsListView;
    private readonly AppConfig appConfig;
    private GpuInfo? gpuInfo;
    private double? temperature;
    private double? powerWatts;

    // Null renders the machine-wide aggregate (the default). Set to an adapter LUID to render just
    // that adapter's slice of GpuInfo.Metrics.Devices / GpuInfo.Specs.Devices.
    private long? scopedAdapterLuid;

    public void SetScope(long? adapterLuid) => scopedAdapterLuid = adapterLuid;

    public GpuPerformanceControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;

        gpuChart = new Chart(terminal);
        gpuDedicatedMemChart = new Chart(terminal);
        gpuMemChart = new Chart(terminal);
        
        gpuMetricsListView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowBorder = true,
            ShowColumnHeaders = true,
            ShowCheckboxes = false,
            Visible = true,
            TabStop = false
        };

        gpuSpecsListView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowBorder = true,
            ShowColumnHeaders = false,
            ShowCheckboxes = false,
            Visible = true,
            TabStop = false
        };
    }

    // Fed every tick, whether or not this pane is on screen, so the three large charts keep a
    // gap-free history the same way the nav mini-charts do.
    public void Sample(SystemSnapshot snapshot)
    {
        lock (@lock) {
            if (snapshot.Gpu == null) {
                return;
            }

            gpuInfo = snapshot.Gpu;
            temperature = snapshot.Thermal?.Metrics.PrimaryTemperature(
                ThermalComponent.Gpu, scopedAdapterLuid?.ToString());
            powerWatts = ResolvePower(snapshot);

            (GpuDeviceMetrics? device, _, bool render) = ResolveScope();

            if (!render) {
                return;
            }

            gpuChart.AddData(device?.GpuPercentTime ?? gpuInfo.Metrics.GpuPercentTime);
            gpuDedicatedMemChart.AddData(
                device?.ToGpuMemoryRatio() ?? gpuInfo.Metrics.ToGpuMemoryRatio());
            gpuMemChart.AddData(
                device?.ToCombinedGpuMemoryRatio() ?? gpuInfo.Metrics.ToCombinedGpuMemoryRatio());
        }
    }

    // Scoped: this adapter's draw. Unscoped (the aggregate panel): every adapter's draw summed.
    private double? ResolvePower(SystemSnapshot snapshot)
    {
        if (snapshot.Power is not { } power) {
            return null;
        }

        if (scopedAdapterLuid is { } luid) {
            return power.Metrics.GpuPower(luid);
        }

        double total = 0;
        bool any = false;

        foreach (GpuDevice spec in snapshot.Gpu?.Specs.Devices ?? []) {
            if (power.Metrics.GpuPower(spec.AdapterLuid) is { } watts) {
                total += watts;
                any = true;
            }
        }

        return any ? total : null;
    }

    // Call while holding @lock, with gpuInfo non-null. render is false only when this pane is
    // scoped to an adapter that is not in the current snapshot.
    private (GpuDeviceMetrics? device, GpuDevice? spec, bool render) ResolveScope()
    {
        if (scopedAdapterLuid is not { } luid) {
            return (null, null, true);
        }

        GpuDeviceMetrics? device = gpuInfo!.Metrics.Devices.FirstOrDefault(candidate => candidate.AdapterLuid == luid);
        GpuDevice? spec = gpuInfo.Specs.Devices.FirstOrDefault(candidate => candidate.AdapterLuid == luid);

        return (device, spec, device != null);
    }

    protected override void OnDraw()
    {
        lock (@lock) {
            if (gpuInfo == null) {
                return;
            }

            (GpuDeviceMetrics? device, GpuDevice? deviceSpec, bool render) = ResolveScope();

            if (!render) {
                return;
            }

            string utilLabel = deviceSpec?.ToDisplayName() ?? "GPU";
            string utilText = device?.ToGpuPercentage() ?? gpuInfo.Metrics.ToGpuPercentage();

            string dedicatedText = device != null
                ? device.ToGpuMemoryPercentage()
                : gpuInfo.Metrics.ToGpuMemoryPercentage();

            string combinedText = device != null
                ? device.ToCombinedGpuMemoryPercentage()
                : gpuInfo.Metrics.ToCombinedGpuMemoryPercentage();

            gpuChart.Text = temperature is { } gpuTemp
                ? $"{utilLabel} {utilText}   ·   {gpuTemp.ToTemperatureText()}"
                : $"{utilLabel} {utilText}";
            gpuChart.Draw();

            gpuDedicatedMemChart.Text = $"Dedicated GPU Memory {dedicatedText}";
            gpuDedicatedMemChart.Draw();

            gpuMemChart.Text = $"GPU Memory {combinedText}";
            gpuMemChart.Draw();

            gpuMetricsListView.Items[0].SubItems[0].Text = utilText;
            gpuMetricsListView.Items[0].SubItems[1].Text = device?.ToGpuMemoryRatioFormattedBytes()
                ?? gpuInfo.Metrics.ToGpuMemoryRatioFormattedBytes();
            gpuMetricsListView.Items[0].SubItems[2].Text = device?.ToCombinedGpuMemoryRatioFormattedBytes()
                ?? gpuInfo.Metrics.ToCombinedGpuMemoryRatioFormattedBytes();
            gpuMetricsListView.Items[0].SubItems[3].Text = device?.ToSharedGpuMemoryRatioFormattedBytes()
                ?? gpuInfo.Metrics.ToSharedGpuMemoryRatioFormattedBytes();
            gpuMetricsListView.Items[0].SubItems[4].Text = powerWatts.ToWattText();
            gpuMetricsListView.Draw();

            // When scoped, the specs list describes that adapter; unscoped (the placeholder panel
            // before the per-device rows arrive), it falls back to the first enumerated adapter.
            GpuDevice? specRows = scopedAdapterLuid is null
                ? gpuInfo.Specs.Devices.FirstOrDefault()
                : deviceSpec;

            gpuSpecsListView.Items[0].SubItems[1].Text = specRows?.Vendor        ?? GpuDeviceParser.NotAvailable;
            gpuSpecsListView.Items[1].SubItems[1].Text = specRows?.Description   ?? GpuDeviceParser.NotAvailable;
            gpuSpecsListView.Items[2].SubItems[1].Text = specRows?.AdapterType   ?? GpuDeviceParser.NotAvailable;
            gpuSpecsListView.Items[3].SubItems[1].Text = specRows?.DriverVersion ?? GpuDeviceParser.NotAvailable;
            gpuSpecsListView.Items[4].SubItems[1].Text = specRows?.DriverDate    ?? GpuDeviceParser.NotAvailable;
            gpuSpecsListView.Draw();
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;
        
        OnLoadChart(gpuChart);
        OnLoadChart(gpuDedicatedMemChart);
        OnLoadChart(gpuMemChart);

        gpuMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Utilization"));
        gpuMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Dedicated GPU Memory"));
        gpuMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("GPU Memory"));
        gpuMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Shared GPU Memory"));
        gpuMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Power Draw"));

        ListViewItem memoryMetricsItem = new(new[] { "0.0%", "0.0/0.0 GB", "0.0/0.0 GB", "0.0/0.0 GB", "N/A" });
        gpuMetricsListView.Items.Add(memoryMetricsItem);
        OnLoadListView(gpuMetricsListView);
        gpuMetricsListView.BorderForegroundColour = appConfig.Theme.ChartBorderForeground;
        gpuMetricsListView.BorderBackgroundColour = appConfig.Theme.ChartBorderBackground;

        gpuSpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));
        gpuSpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));

        gpuSpecsListView.Items.Add(new ListViewItem(new[] { "Vendor:",         GpuDeviceParser.NotAvailable }));
        gpuSpecsListView.Items.Add(new ListViewItem(new[] { "Description:",    GpuDeviceParser.NotAvailable }));
        gpuSpecsListView.Items.Add(new ListViewItem(new[] { "Adapter Type:",   GpuDeviceParser.NotAvailable }));
        gpuSpecsListView.Items.Add(new ListViewItem(new[] { "Driver Version:", GpuDeviceParser.NotAvailable }));
        gpuSpecsListView.Items.Add(new ListViewItem(new[] { "Driver Date:",    GpuDeviceParser.NotAvailable }));
        OnLoadListView(gpuSpecsListView);
        gpuSpecsListView.BorderForegroundColour = appConfig.Theme.ChartBorderForeground;
        gpuSpecsListView.BorderBackgroundColour = appConfig.Theme.ChartBorderBackground;
    }

    private void OnLoadChart(Chart chart)
    {
        chart.AutoScale = false;
        chart.BackgroundColour = appConfig.Theme.ChartBackground;
        chart.ForegroundColour = appConfig.Theme.Foreground;
        chart.CustomYAxisScaleFormatter = Chart.FormatYScalePercentage;
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
        int yTop = Y;

        // The metrics and specs list views are a fixed height and anchored to the bottom of the
        // control; the three charts grow to share whatever height is left above them. Every
        // element is stacked flush against the next, with no gap rows between them.
        // +2 over the single detail row (plus header) for the metrics list's own top/bottom border.
        const int MetricsHeight = 4;

        // +3 over the field row count: two for the specs list's own top/bottom border, one
        // because RowCount is Bounds.Height - 1.
        int specsHeight = SpecsRowCount + 3;
        int bottomY = Y + Height - (MetricsHeight + specsHeight);

        int chartsArea = Math.Max(0, bottomY - yTop);
        int height = chartsArea / 3;

        gpuChart.X = X + 1;
        gpuChart.Y = yTop;
        gpuChart.Width = Width - 1;
        gpuChart.Height = height;
        gpuChart.Resize();

        yTop += height;

        gpuDedicatedMemChart.X = X + 1;
        gpuDedicatedMemChart.Y = yTop;
        gpuDedicatedMemChart.Width = Width - 1;
        gpuDedicatedMemChart.Height = height;
        gpuDedicatedMemChart.Resize();

        yTop += height;

        gpuMemChart.X = X + 1;
        gpuMemChart.Y = yTop;
        gpuMemChart.Width = Width - 1;
        gpuMemChart.Height = chartsArea - 2 * height;
        gpuMemChart.Resize();

        gpuMetricsListView.ColumnHeaders[0].Width = 15;
        gpuMetricsListView.ColumnHeaders[1].Width = 24;
        gpuMetricsListView.ColumnHeaders[2].Width = 22;
        gpuMetricsListView.ColumnHeaders[3].Width = 22;
        gpuMetricsListView.ColumnHeaders[4].Width = 12;

        // Keys on the left, their values on the right. The value column is wider than the disk
        // specs list because a GPU description or driver string runs long.
        for (int i = 0; i < gpuSpecsListView.ColumnHeaders.Count(); i++) {
            gpuSpecsListView.ColumnHeaders[i].Width = i % 2 == 0
                ? 18
                : 50;
        }

        gpuMetricsListView.X = X + 1;
        gpuMetricsListView.Y = bottomY;
        gpuMetricsListView.Width = Width - 1;
        gpuMetricsListView.Height = MetricsHeight;

        gpuSpecsListView.X = X + 1;
        gpuSpecsListView.Y = bottomY + MetricsHeight;
        gpuSpecsListView.Width = Width - 1;

        // ListView.DrawItems renders Height - 1 rows, so the last row is clipped without the
        // extra line.
        gpuSpecsListView.Height = specsHeight;
    }

    protected override void OnUnload()
    {
        gpuMetricsListView.ColumnHeaders.Clear();
        gpuMetricsListView.Items.Clear();

        gpuSpecsListView.ColumnHeaders.Clear();
        gpuSpecsListView.Items.Clear();
    }
}