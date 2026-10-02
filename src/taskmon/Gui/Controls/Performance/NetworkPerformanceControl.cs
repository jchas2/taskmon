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
    private const int SpecsRowCount = 15;

    private readonly Lock @lock = new();
    private readonly Chart sendChart;
    private readonly Chart receiveChart;
    private readonly ListView networkMetricsListView;
    private readonly ListView networkSpecsListView;
    private readonly AppConfig appConfig;
    
    private NetworkInfo? networkInfo;
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
            ShowBorder = true,
            ShowColumnHeaders = true,
            ShowCheckboxes = false,
            Visible = true,
            TabStop = false
        };

        networkSpecsListView = new ListView(terminal) {
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

            string totalBytesSent = (device?.TotalBytesSent ?? metrics.TotalBytesSent).ToFormattedByteSize();
            string totalBytesReceived = (device?.TotalBytesReceived ?? metrics.TotalBytesReceived).ToFormattedByteSize();
            string totalPacketsSent = (device?.TotalPacketsSent ?? metrics.TotalPacketsSent).ToNetworkPacketCount();
            string totalPacketsReceived = (device?.TotalPacketsReceived ?? metrics.TotalPacketsReceived).ToNetworkPacketCount();

            if (device != null) {
                SetSpecsRow(0,  "Name:",                   spec?.Name               ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(1,  "Friendly Name:",          spec?.FriendlyName       ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(2,  "Description:",            spec?.Description        ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(3,  "Connection Type:",        spec?.ConnectionType     ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(4,  "Physical Medium:",        spec?.PhysicalMedium     ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(5,  "MAC Address:",            spec?.MacAddress         ?? NetworkDeviceParser.NotAvailable);
                SetSpecsRow(6,  "IPv4 Addresses:",         (spec?.IPv4Addresses     ?? []).ToAddressList());
                SetSpecsRow(7,  "IPv6 Addresses:",         (spec?.IPv6Addresses     ?? []).ToAddressList());
                SetSpecsRow(8,  "Transmit Link Speed:",    (spec?.TransmitLinkSpeed ?? 0UL).ToLinkSpeed());
                SetSpecsRow(9,  "Receive Link Speed:",     (spec?.ReceiveLinkSpeed  ?? 0UL).ToLinkSpeed());
                SetSpecsRow(10, "Operational Status:",     spec?.OperationalStatus  ?? NetworkDeviceParser.NotAvailable);
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
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

        OnLoadChart(sendChart);
        OnLoadChart(receiveChart);

        networkMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Send"));
        networkMetricsListView.ColumnHeaders.Add(new ListViewColumnHeader("Receive"));

        ListViewItem networkMetricsItem = new(new[] { "0.0 B/s", "0.0 B/s" });
        networkMetricsListView.Items.Add(networkMetricsItem);
        
        OnLoadListView(networkMetricsListView);
        
        networkMetricsListView.BorderForegroundColour = appConfig.Theme.ChartBorderForeground;
        networkMetricsListView.BorderBackgroundColour = appConfig.Theme.ChartBorderBackground;

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
        
        networkSpecsListView.BorderForegroundColour = appConfig.Theme.ChartBorderForeground;
        networkSpecsListView.BorderBackgroundColour = appConfig.Theme.ChartBorderBackground;
    }

    private void OnLoadChart(Chart chart)
    {
        chart.AutoScale = true;
        chart.BackgroundColour = appConfig.Theme.ChartBackground;
        chart.ForegroundColour = appConfig.Theme.Foreground;
        chart.CustomYAxisScaleFormatter = PerformanceChartFormatters.FormatYScaleByteRate;
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

        for (int i = 0; i < networkSpecsListView.ColumnHeaders.Count(); i++) {
            networkSpecsListView.ColumnHeaders[i].Width = i % 2 == 0
                ? 25
                : 50;
        }

        networkMetricsListView.X = X + 1;
        networkMetricsListView.Y = bottomY;
        networkMetricsListView.Width = Width - 1;
        networkMetricsListView.Height = MetricsHeight;

        networkSpecsListView.X = X + 1;
        networkSpecsListView.Y = bottomY + MetricsHeight;
        networkSpecsListView.Width = Width - 1;
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
