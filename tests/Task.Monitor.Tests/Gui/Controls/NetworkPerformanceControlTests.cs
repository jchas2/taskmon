using Moq;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class NetworkPerformanceControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public NetworkPerformanceControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private static NetworkInfo BuildNetworkInfo() =>
        new() {
            Specs = new NetworkSpecs {
                Devices = [
                    new NetworkDevice {
                        InterfaceIndex = 5,
                        FriendlyName = "Ethernet 3",
                        Description = "Realtek Gaming 2.5GbE Family Controller #2",
                        ConnectionType = "Ethernet",
                        IsActive = true,
                        CountsTowardAggregate = true,
                    },
                ],
            },
            Metrics = new NetworkMetrics {
                SendBytesPerSecond = 512.0 * 1024,
                ReceiveBytesPerSecond = 28.5 * 1024 * 1024,
                SendMegabytesPerSecond = 0.5,
                ReceiveMegabytesPerSecond = 28.5,
                TotalBytesSent = 1782579,
                TotalBytesReceived = 55037657,
                TotalPacketsSent = 20050161,
                TotalPacketsReceived = 28475940,
            },
        };

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        NetworkPerformanceControl ctrl = new(runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Should_Draw_Send_And_Receive_Throughput()
    {
        // Wrap the mock terminal so the Chart's ReadOnlySpan<char> blit doesn't hit the Moq
        // proxy (which can't proxy ref-struct params); other writes still forward to the mock.
        NetworkPerformanceControl ctrl = new(
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 120,
            Height = 36
        };

        ctrl.Load();
        ctrl.Resize();
        ctrl.Sample(new SystemSnapshot { Network = BuildNetworkInfo() });
        ctrl.Draw();

        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("Send"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("Receive"))), Times.AtLeastOnce);

        // 512 KB/s up, 28.5 MB/s down.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("512.0 KB/s"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("28.5 MB/s"))), Times.AtLeastOnce);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Should_Draw_Network_Totals_Keys_And_Values()
    {
        NetworkPerformanceControl ctrl = new(
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 120,
            Height = 36
        };

        ctrl.Load();
        ctrl.Resize();
        ctrl.Sample(new SystemSnapshot { Network = BuildNetworkInfo() });
        ctrl.Draw();

        // All four rows have to render: ListView.DrawItems draws Height - 1 rows, so the last
        // one is clipped if the list view is sized to its row count.
        foreach (string key in new[] {
            "Total Bytes Sent:",
            "Total Bytes Received:",
            "Total Packets Sent:",
            "Total Packets Received:" }) {

            runContextHelper.terminal.Verify(
                t => t.Write(It.Is<string>(s => s.Contains(key))), Times.AtLeastOnce);
        }

        // 1782579 bytes sent, 55037657 received.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("1.7 MB"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("52.5 MB"))), Times.AtLeastOnce);

        // Packets are a count, not a size, so they keep their digits.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("20,050,161"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("28,475,940"))), Times.AtLeastOnce);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    private static NetworkInfo BuildNetworkInfoWithDevices()
    {
        NetworkInfo info = BuildNetworkInfo();

        info.Metrics.Devices =
        [
            new NetworkDeviceMetrics {
                InterfaceIndex = 5,
                InterfaceLuid = 111,
                FriendlyName = "Ethernet",
                SendBytesPerSecond = 1.0 * 1024 * 1024,
                ReceiveBytesPerSecond = 2.0 * 1024 * 1024,
                TotalBytesSent = 10_000_000,
                TotalBytesReceived = 20_000_000,
                TotalPacketsSent = 111,
                TotalPacketsReceived = 222,
            },
            new NetworkDeviceMetrics {
                InterfaceIndex = 9,
                InterfaceLuid = 222,
                FriendlyName = "Wi-Fi",
                SendBytesPerSecond = 8.0 * 1024 * 1024,
                ReceiveBytesPerSecond = 9.0 * 1024 * 1024,
                TotalBytesSent = 30_000_000,
                TotalBytesReceived = 40_000_000,
                TotalPacketsSent = 333,
                TotalPacketsReceived = 444,
            },
        ];

        info.Specs.Devices =
        [
            new NetworkDevice {
                InterfaceIndex = 9,
                InterfaceLuid = 222,
                Name = "{GUID-WIFI}",
                FriendlyName = "Wi-Fi",
                Description = "Intel Wi-Fi 6E AX211 160MHz",
                ConnectionType = "Wi-Fi",
                PhysicalMedium = "Native 802.11",
                MacAddress = "AA-BB-CC-DD-EE-FF",
                IPv4Addresses = ["192.168.1.50", "192.168.1.51"],
                IPv6Addresses = ["fe80::1", "2001:db8::2"],
                TransmitLinkSpeed = 1_200_000_000,
                ReceiveLinkSpeed = 1_200_000_000,
                OperationalStatus = "Up",
                IsActive = true,
                CountsTowardAggregate = true,
            },
        ];

        return info;
    }

    [Fact]
    public void Scoped_To_An_Adapter_Draws_That_Adapter_Only()
    {
        NetworkPerformanceControl ctrl = new(
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 120,
            Height = 44
        };

        ctrl.SetScope(222UL);
        ctrl.Load();
        ctrl.Resize();
        ctrl.Sample(new SystemSnapshot { Network = BuildNetworkInfoWithDevices() });
        ctrl.Draw();

        // Wi-Fi: 8.0 MB/s up, 9.0 MB/s down.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("8.0 MB/s"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("9.0 MB/s"))), Times.AtLeastOnce);

        // The Ethernet adapter's rates must not appear in a Wi-Fi view.
        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("1.0 MB/s"))), Times.Never);

        foreach (string key in new[] {
            "Name:", "Friendly Name:", "Description:", "Connection Type:", "Physical Medium:",
            "MAC Address:", "IPv4 Addresses:", "IPv6 Addresses:", "Transmit Link Speed:",
            "Receive Link Speed:", "Operational Status:" }) {

            runContextHelper.terminal.Verify(
                t => t.Write(It.Is<string>(s => s.Contains(key))), Times.AtLeastOnce);
        }

        foreach (string value in new[] {
            "Intel Wi-Fi 6E AX211 160MHz", "Native 802.11", "AA-BB-CC-DD-EE-FF",
            "192.168.1.50, 192.168.1.51", "fe80::1, 2001:db8::2", "1.2 Gbps", "Up" }) {

            runContextHelper.terminal.Verify(
                t => t.Write(It.Is<string>(s => s.Contains(value))), Times.AtLeastOnce);
        }

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Should_Not_Draw_Before_A_Snapshot_Arrives()
    {
        NetworkPerformanceControl ctrl = new(
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = 120,
            Height = 36
        };

        ctrl.Load();
        ctrl.Resize();
        ctrl.Draw();

        runContextHelper.terminal.Verify(
            t => t.Write(It.Is<string>(s => s.Contains("Total Bytes Sent:"))), Times.Never);

        ctrl.Unload();
    }
}
