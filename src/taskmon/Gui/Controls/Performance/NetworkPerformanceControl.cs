using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Network;

namespace Task.Monitor.Gui.Controls.Performance;

public sealed class NetworkPerformanceControl : Control, IPerformanceDetail
{
    // The scoped view is the wide one: eleven adapter fields then the four cumulative totals. The
    // aggregate view fills only the four totals and blanks the rest. Fixed rather than taken from
    // Items.Count so a resize that lands before the load still sizes correctly.
    private const int SpecsRowCount = 15;

    private readonly Lock @lock = new();
    private readonly Chart sendChart;
    private readonly Chart receiveChart;
    private readonly ListView networkMetricsListView;
    private readonly ListView networkSpecsListView;
    private readonly AppConfig appConfig;
    private NetworkInfo? networkInfo;

    // Null sums across every active adapter (the default). Set to an interface LUID to render just
    // that adapter's slice of NetworkInfo.Metrics.Devices.
    private ulong? scopedInterfaceLuid;

    public void SetScope(ulong? interfaceLuid) => scopedInterfaceLuid = interfaceLuid;

    public NetworkPerformanceControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;

        sendChart = new Chart(terminal);
        receiveChart = new Chart(terminal);

        networkMetricsListView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowBorder = false,
            ShowColumnHeaders = true,
            ShowCheckboxes = false,
            Visible = true,
            TabStop = false
        };

        networkSpecsListView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowBorder = false,
            ShowColumnHeaders = false,
            ShowCheckboxes = false,
            Visible = true,
            TabStop = false
        };
    }

    // Fed every tick, whether or not this pane is on screen, so the two large charts keep a
    // gap-free history the same way the nav mini-charts do.
    public void Sample(SystemSnapshot snapshot)
    {
        lock (@lock) {
            if (snapshot.Network == null) {
                return;
            }

            networkInfo = snapshot.Network;

            (NetworkDeviceMetrics? device, _, bool render) = ResolveScope();

            if (!render) {
                return;
            }

            sendChart.AddData(device?.SendBytesPerSecond ?? networkInfo.Metrics.SendBytesPerSecond);
            receiveChart.AddData(device?.ReceiveBytesPerSecond ?? networkInfo.Metrics.ReceiveBytesPerSecond);
        }
    }

    // Call while holding @lock, with networkInfo non-null. render is false only when this pane is
    // scoped to an adapter that is not in the current snapshot.
    private (NetworkDeviceMetrics? device, NetworkDevice? spec, bool render) ResolveScope()
    {
        if (scopedInterfaceLuid is not { } luid) {
            return (null, null, true);
        }

        NetworkDeviceMetrics? device = networkInfo!.Metrics.Devices.FirstOrDefault(candidate => candidate.InterfaceLuid == luid);
        NetworkDevice? spec = networkInfo.Specs.Devices.FirstOrDefault(candidate => candidate.InterfaceLuid == luid);

        return (device, spec, device != null);
    }

    private void SetSpecsRow(int index, string label, string value)
    {
        networkSpecsListView.Items[index].SubItems[0].Text = label;
        networkSpecsListView.Items[index].SubItems[1].Text = value;
    }

    protected override void OnDraw()
    {
        lock (@lock) {
            if (networkInfo == null) {
                return;
            }

            NetworkMetrics metrics = networkInfo.Metrics;

            (NetworkDeviceMetrics? device, NetworkDevice? spec, bool render) = ResolveScope();

            if (!render) {
                return;
            }

            string sendText = device?.ToNetworkSendRate() ?? metrics.ToNetworkSendRate();
            string receiveText = device?.ToNetworkReceiveRate() ?? metrics.ToNetworkReceiveRate();

            sendChart.Text = $"Send {sendText}";
            sendChart.Draw();

            receiveChart.Text = $"Receive {receiveText}";
            receiveChart.Draw();

            networkMetricsListView.Items[0].SubItems[0].Text = sendText;
            networkMetricsListView.Items[0].SubItems[1].Text = receiveText;
            networkMetricsListView.Draw();

            // The byte and packet totals are cumulative since the service started, not since
            // boot: the service accumulates per cycle deltas rather than tracking an absolute
            // baseline.
            string totalBytesSent = (device?.TotalBytesSent ?? metrics.TotalBytesSent).ToFormattedByteSize();
            string totalBytesReceived = (device?.TotalBytesReceived ?? metrics.TotalBytesReceived).ToFormattedByteSize();
            string totalPacketsSent = (device?.TotalPacketsSent ?? metrics.TotalPacketsSent).ToNetworkPacketCount();
            string totalPacketsReceived = (device?.TotalPacketsReceived ?? metrics.TotalPacketsReceived).ToNetworkPacketCount();

            if (device != null) {
                SetSpecsRow(0,  "Name:",                spec?.Name              ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(1,  "Friendly Name:",       spec?.FriendlyName       ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(2,  "Description:",         spec?.Description        ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(3,  "Connection Type:",     spec?.ConnectionType     ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(4,  "Physical Medium:",     spec?.PhysicalMedium     ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(5,  "MAC Address:",         spec?.MacAddress         ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(6,  "IPv4 Addresses:",      (spec?.IPv4Addresses     ?? []).ToAddressList());
                SetSpecsRow(7,  "IPv6 Addresses:",      (spec?.IPv6Addresses     ?? []).ToAddressList());
                SetSpecsRow(8,  "Transmit Link Speed:", (spec?.TransmitLinkSpeed ?? 0UL).ToLinkSpeed());
                SetSpecsRow(9,  "Receive Link Speed:",  (spec?.ReceiveLinkSpeed  ?? 0UL).ToLinkSpeed());
                SetSpecsRow(10, "Operational Status:",  spec?.OperationalStatus  ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(11, "Total Bytes Sent:",       totalBytesSent);
                SetSpecsRow(12, "Total Bytes Received:",   totalBytesReceived);
                SetSpecsRow(13, "Total Packets Sent:",     totalPacketsSent);
                SetSpecsRow(14, "Total Packets Received:", totalPacketsReceived);
            }
            else {
                SetSpecsRow(0, "Total Bytes Sent:",       totalBytesSent);
                SetSpecsRow(1, "Total Bytes Received:",   totalBytesReceived);
                SetSpecsRow(2, "Total Packets Sent:",     totalPacketsSent);
                SetSpecsRow(3, "Total Packets Received:", totalPacketsReceived);

                for (int row = 4; row < networkSpecsListView.Items.Count; row++) {
                    SetSpecsRow(row, string.Empty, string.Empty);
                }
            }

            networkSpecsListView.Draw();
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        // Throughput has no ceiling to scale against, so both charts find their own and label the
        // Y axis in byte rates rather than as a percentage. Send and receive are plotted
        // separately rather than stacked so an asymmetric link reads correctly.
        OnLoadChart(sendChart);
        OnLoadChart(receiveChart);

        networkMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Send"));
        networkMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Receive"));

        ListViewItem networkMetricsItem = new(new[] { "0.0 B/s", "0.0 B/s" });
        networkMetricsListView.Items.Add(networkMetricsItem);
        OnLoadListView(networkMetricsListView);

        networkSpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));
        networkSpecsListView.ColumnHeaders.Add(new ListViewColumnHeader(""));

        foreach (string label in new[] {
            "Name:", "Friendly Name:", "Description:", "Connection Type:", "Physical Medium:",
            "MAC Address:", "IPv4 Addresses:", "IPv6 Addresses:", "Transmit Link Speed:",
            "Receive Link Speed:", "Operational Status:", "Total Bytes Sent:", "Total Bytes Received:",
            "Total Packets Sent:", "Total Packets Received:" }) {

            networkSpecsListView.Items.Add(new ListViewItem(new[] { label, NetworkDeviceParser.NotAvailable }));
        }

        OnLoadListView(networkSpecsListView);
    }

    private void OnLoadChart(Chart chart)
    {
        chart.AutoScale = true;
        chart.BackgroundColour = appConfig.DefaultTheme.Background;
        chart.ForegroundColour = appConfig.DefaultTheme.Foreground;
        chart.CustomYAxisScaleFormatter = PerformanceChartFormatters.FormatYScaleByteRate;
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

        // The metrics and specs list views are a fixed height and anchored to the bottom of the
        // control; the two charts grow to share whatever height is left above them.
        const int MetricsHeight = 2;
        const int SpecsGap = 1;
        int specsHeight = SpecsRowCount + 1;
        int bottomY = Y + Height - (MetricsHeight + SpecsGap + specsHeight);

        // One row is held back for the gap between the charts and the bottom list stack.
        int chartsArea = Math.Max(0, bottomY - yTop - 1);
        int height = chartsArea / 2;

        sendChart.X = X + 1;
        sendChart.Y = yTop;
        sendChart.Width = Width - 1;
        sendChart.Height = height;
        sendChart.Resize();

        yTop += height;

        receiveChart.X = X + 1;
        receiveChart.Y = yTop;
        receiveChart.Width = Width - 1;
        receiveChart.Height = chartsArea - height;
        receiveChart.Resize();

        networkMetricsListView.ColumnHeaders[0].Width = 18;
        networkMetricsListView.ColumnHeaders[1].Width = 18;

        // Keys on the left, values on the right. The value column is wide enough for a joined
        // address list or a full adapter description.
        for (int i = 0; i < networkSpecsListView.ColumnHeaders.Count(); i++) {
            networkSpecsListView.ColumnHeaders[i].Width = i % 2 == 0
                ? 25
                : 50;
        }

        networkMetricsListView.X = X + 2;
        networkMetricsListView.Y = bottomY;
        networkMetricsListView.Width = Width - 3;
        networkMetricsListView.Height = MetricsHeight;

        networkSpecsListView.X = X + 2;
        networkSpecsListView.Y = bottomY + MetricsHeight + SpecsGap;
        networkSpecsListView.Width = Width - 3;

        // ListView.DrawItems renders Height - 1 rows, so the last row is clipped without the
        // extra line.
        networkSpecsListView.Height = specsHeight;
    }

    protected override void OnUnload()
    {
        networkMetricsListView.ColumnHeaders.Clear();
        networkMetricsListView.Items.Clear();

        networkSpecsListView.ColumnHeaders.Clear();
        networkSpecsListView.Items.Clear();
    }
}
