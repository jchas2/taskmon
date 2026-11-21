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
using Task.Monitor.System.Services.Network;
using Constants = Task.Monitor.System.Services.Constants;

namespace Task.Monitor.Gui.Controls.Performance;

public sealed class NetworkPerformanceControl : Control, IPerformanceDetail
{
    private const int SpecsRowCount = 15;
    
    private readonly Chart     sendChart;
    private readonly Chart     receiveChart;
    private readonly ListView  networkMetricsListView;
    private readonly ListView  networkSpecsListView;
    private readonly AppConfig appConfig;
    
    private NetworkInfo? networkInfo;

    public NetworkPerformanceControl(ISystemTerminal terminal, AppConfig appConfig, ulong? interfaceLuid = null) : base(terminal)
    {
        this.appConfig = appConfig;

        InterfaceLuid = interfaceLuid;
        Spark         = new Chart(terminal) { CustomYAxisScaleFormatter = Chart.FormatYScaleByteRate };
        Title         = Strings.PerformanceNetworkPanel;

        sendChart    = new Chart(terminal);
        receiveChart = new Chart(terminal);

        networkMetricsListView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowBorder        = true,
            ShowColumnHeaders = true,
            ShowCheckboxes    = false,
            Visible           = true,
            TabStop           = false
        };
        
        networkMetricsListView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.PerformanceSendColumn))
            .Add(new ListViewColumnHeader(Strings.PerformanceReceiveColumn));

        networkSpecsListView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowBorder        = true,
            ShowColumnHeaders = false,
            ShowCheckboxes    = false,
            Visible           = true,
            TabStop           = false
        };
        
        networkSpecsListView.ColumnHeaders
            .Add(new ListViewColumnHeader(""))
            .Add(new ListViewColumnHeader(""));
    }

    public ulong? InterfaceLuid { get; }

    protected override void OnDraw()
    {
        if (networkInfo == null) {
            return;
        }

        NetworkMetrics metrics = networkInfo.Metrics;

        (NetworkDeviceMetrics? device, NetworkDevice? spec, bool render) = ResolveScope();

        if (!render) {
            return;
        }

        string sendText    = device?.ToNetworkSendRate() ?? metrics.ToNetworkSendRate();
        string receiveText = device?.ToNetworkReceiveRate() ?? metrics.ToNetworkReceiveRate();

        sendChart.Text = string.Format(CultureInfo.CurrentCulture, Strings.PerformanceSendChart, sendText);
        sendChart.Draw();

        receiveChart.Text = string.Format(CultureInfo.CurrentCulture, Strings.PerformanceReceiveChart, receiveText);
        receiveChart.Draw();

        networkMetricsListView.Items[0].SubItems[0].Text = sendText;
        networkMetricsListView.Items[0].SubItems[1].Text = receiveText;
        networkMetricsListView.Draw();

        string totalBytesSent       = (device?.TotalBytesSent ?? metrics.TotalBytesSent).ToFormattedByteSize();
        string totalBytesReceived   = (device?.TotalBytesReceived ?? metrics.TotalBytesReceived).ToFormattedByteSize();
        string totalPacketsSent     = (device?.TotalPacketsSent ?? metrics.TotalPacketsSent).ToNetworkPacketCount();
        string totalPacketsReceived = (device?.TotalPacketsReceived ?? metrics.TotalPacketsReceived).ToNetworkPacketCount();

        if (device != null) {
            SetSpecsRow(0,  Strings.LabelName,                       spec?.Name               ?? Constants.NotAvailable);
            SetSpecsRow(1,  Strings.PerformanceFriendlyName,         spec?.FriendlyName       ?? Constants.NotAvailable);
            SetSpecsRow(2,  Strings.LabelDescription,                spec?.Description        ?? Constants.NotAvailable);
            SetSpecsRow(3,  Strings.LabelConnectionType,             spec?.ConnectionType     ?? Constants.NotAvailable);
            SetSpecsRow(4,  Strings.LabelPhysicalMedium,             spec?.PhysicalMedium     ?? Constants.NotAvailable);
            SetSpecsRow(5,  Strings.LabelMacAddress,                 spec?.MacAddress         ?? Constants.NotAvailable);
            SetSpecsRow(6,  Strings.LabelIpv4Addresses,              (spec?.IPv4Addresses     ?? []).ToAddressList());
            SetSpecsRow(7,  Strings.LabelIpv6Addresses,              (spec?.IPv6Addresses     ?? []).ToAddressList());
            SetSpecsRow(8,  Strings.LabelTransmitLinkSpeed,          (spec?.TransmitLinkSpeed ?? 0UL).ToLinkSpeed());
            SetSpecsRow(9,  Strings.LabelReceiveLinkSpeed,           (spec?.ReceiveLinkSpeed  ?? 0UL).ToLinkSpeed());
            SetSpecsRow(10, Strings.LabelOperationalStatus,          spec?.OperationalStatus  ?? Constants.NotAvailable);
            SetSpecsRow(11, Strings.PerformanceTotalBytesSent,       totalBytesSent);
            SetSpecsRow(12, Strings.PerformanceTotalBytesReceived,   totalBytesReceived);
            SetSpecsRow(13, Strings.PerformanceTotalPacketsSent,     totalPacketsSent);
            SetSpecsRow(14, Strings.PerformanceTotalPacketsReceived, totalPacketsReceived);
        }
        else {
            SetSpecsRow(0, Strings.PerformanceTotalBytesSent,       totalBytesSent);
            SetSpecsRow(1, Strings.PerformanceTotalBytesReceived,   totalBytesReceived);
            SetSpecsRow(2, Strings.PerformanceTotalPacketsSent,     totalPacketsSent);
            SetSpecsRow(3, Strings.PerformanceTotalPacketsReceived, totalPacketsReceived);

            for (int row = 4; row < networkSpecsListView.Items.Count; row++) {
                SetSpecsRow(row, string.Empty, string.Empty);
            }
        }

        networkSpecsListView.Draw();
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(networkMetricsListView, appConfig.Theme);
        CommonControl.Theme(networkSpecsListView, appConfig.Theme);

        OnLoadChart(sendChart);
        OnLoadChart(receiveChart);
        
        ListViewItem networkMetricsItem = new(new[] { "0.0 B/s", "0.0 B/s" });
        networkMetricsListView.Items.Add(networkMetricsItem);

        foreach (string label in new[] {
            Strings.LabelName, 
            Strings.PerformanceFriendlyName, 
            Strings.LabelDescription, 
            Strings.LabelConnectionType, 
            Strings.LabelPhysicalMedium,
            Strings.LabelMacAddress, 
            Strings.LabelIpv4Addresses, 
            Strings.LabelIpv6Addresses, 
            Strings.LabelTransmitLinkSpeed,
            Strings.LabelReceiveLinkSpeed, 
            Strings.LabelOperationalStatus, 
            Strings.PerformanceTotalBytesSent,
            Strings.PerformanceTotalBytesReceived,
            Strings.PerformanceTotalPacketsSent, 
            Strings.PerformanceTotalPacketsReceived }) {

            networkSpecsListView.Items.Add(CommonControl.PropertyRow(label, Constants.NotAvailable, appConfig.Theme));
        }
    }

    private void OnLoadChart(Chart chart)
    {
        CommonControl.Theme(chart, appConfig.Theme);
        
        chart.AutoScale                 = true;
        chart.CustomYAxisScaleFormatter = Chart.FormatYScaleByteRate;
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
        int chartsArea = Math.Max(0, bottomY - yTop);
        int height     = chartsArea / 2;

        sendChart.X      = X + 1;
        sendChart.Y      = yTop;
        sendChart.Width  = Width - 1;
        sendChart.Height = height;
        
        sendChart.Resize();

        yTop += height;

        receiveChart.X      = X + 1;
        receiveChart.Y      = yTop;
        receiveChart.Width  = Width - 1;
        receiveChart.Height = chartsArea - height;
        
        receiveChart.Resize();

        networkMetricsListView.ColumnHeaders[0].Width = 18;
        networkMetricsListView.ColumnHeaders[1].Width = 18;

        for (int i = 0; i < networkSpecsListView.ColumnHeaders.Count(); i++) {
            networkSpecsListView.ColumnHeaders[i].Width = i % 2 == 0
                ? 25
                : 50;
        }

        networkMetricsListView.X      = X + 1;
        networkMetricsListView.Y      = bottomY;
        networkMetricsListView.Width  = Width - 1;
        networkMetricsListView.Height = MetricsHeight;

        networkSpecsListView.X      = X + 1;
        networkSpecsListView.Y      = bottomY + MetricsHeight;
        networkSpecsListView.Width  = Width - 1;
        networkSpecsListView.Height = specsHeight;
    }

    protected override void OnUnload()
    {
        networkMetricsListView.Items.Clear();
        networkSpecsListView.Items.Clear();
    }

    private (NetworkDeviceMetrics? device, NetworkDevice? spec, bool render) ResolveScope()
    {
        if (InterfaceLuid is not { } luid) {
            return (null, null, true);
        }

        NetworkDeviceMetrics? device = networkInfo!.Metrics.Devices.FirstOrDefault(candidate => candidate.InterfaceLuid == luid);
        NetworkDevice? spec          = networkInfo.Specs.Devices.FirstOrDefault(candidate => candidate.InterfaceLuid == luid);

        return (device, spec, device != null);
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Network == null) {
            return;
        }

        networkInfo = snapshot.Network;

        (NetworkDeviceMetrics? device, NetworkDevice? spec, bool render) = ResolveScope();

        if (!render) {
            return;
        }

        if (device is not null) {
            Title = spec?.ToDisplayName() ?? device.ToDisplayName();
        }

        Summary = (string.Format(
                CultureInfo.CurrentCulture, 
                Strings.PerformanceSendShort, 
                device?.ToNetworkSendRate() ?? networkInfo.Metrics.ToNetworkSendRate()),
            string.Format(
                CultureInfo.CurrentCulture, 
                Strings.PerformanceReceiveShort, 
                device?.ToNetworkReceiveRate() ?? networkInfo.Metrics.ToNetworkReceiveRate()));

        sendChart.AddData(device?.SendBytesPerSecond ?? networkInfo.Metrics.SendBytesPerSecond);
        receiveChart.AddData(device?.ReceiveBytesPerSecond ?? networkInfo.Metrics.ReceiveBytesPerSecond);
        Spark.AddData(device?.ToNetworkThroughputBytesPerSecond() ?? networkInfo.Metrics.ToNetworkThroughputBytesPerSecond());
    }

    private void SetSpecsRow(int index, string label, string value)
    {
        networkSpecsListView.Items[index].SubItems[0].Text = label;
        networkSpecsListView.Items[index].SubItems[1].Text = value;
    }

    public Chart Spark { get; }

    public (string Top, string Bottom) Summary { get; private set; } = (string.Empty, string.Empty);

    public string Title { get; private set; }
}
