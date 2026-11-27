using System.Drawing;
using System.Reflection;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Gui.Controls.Disk;
using Task.Monitor.Gui.Controls.Gpu;
using Task.Monitor.Gui.Controls.Network;
using Task.Monitor.Gui.Controls.Summary2;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.System;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Services.Npu;
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Keys = Task.Monitor.Configuration.Constants.Keys;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

// What each kind of pane is made as, and how the charts and metres show a snapshot. Each test shows
// a one-pane tree and publishes snapshots as the service controller does.
public sealed class SummaryPaneTests
{
    private const double Gibibyte = 1024.0 * 1024.0 * 1024.0;
    private const double Mebibyte = 1024.0 * 1024.0;

    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public SummaryPaneTests()
    {
        runContextHelper = new RunContextHelper();
        runContext       = runContextHelper.GetRunContext();
    }

    // Each key is set to a colour nothing else uses, so a chart themed from the wrong key fails.
    [Fact]
    public void A_Chart_Pane_Takes_The_Chart_Colours()
    {
        ConfigSection themeSection = new("Summary Pane Theme");
        themeSection.Add(Keys.Foreground, "#202020");
        themeSection.Add(Keys.ChartBackground, "#303030");
        themeSection.Add(Keys.ChartBorderForeground, "#404040");
        themeSection.Add(Keys.ChartBorderBackground, "#505050");
        themeSection.Add(Keys.ChartYAxis, "#606060");
        runContext.AppConfig.Theme.Update(themeSection);

        (SummaryControl2 summary, Control pane) = Shown(PaneControlType.Cpu, height: 10);
        Chart chart = Assert.IsType<Chart>(pane);

        Assert.Equal(ColorTranslator.FromHtml("#303030").ToArgb(), chart.BackgroundColour.ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#202020").ToArgb(), chart.ForegroundColour.ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#404040").ToArgb(), chart.BorderForegroundColour.ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#505050").ToArgb(), chart.BorderBackgroundColour.ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#606060").ToArgb(), chart.YAxisColour.ToArgb());

        summary.Unload();
    }

    [Fact]
    public void A_Metre_Ignores_A_Snapshot_Without_Its_Data()
    {
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(PaneControlType.MemoryMetre);

        Publish(new SystemSnapshot());

        Assert.Equal("Memory", metre.Text);
        Assert.Equal(0.0, metre.Series[0].Value);

        summary.Unload();
    }

    [Theory]
    [InlineData(PaneControlType.CpuMetre)]
    [InlineData(PaneControlType.GpuMetre)]
    [InlineData(PaneControlType.GpuMemoryMetre)]
    [InlineData(PaneControlType.MemoryMetre)]
    [InlineData(PaneControlType.VirtualMemoryMetre)]
    [InlineData(PaneControlType.DiskMetre)]
    [InlineData(PaneControlType.NetworkSendMetre)]
    [InlineData(PaneControlType.NetworkReceiveMetre)]
    public void A_Metre_Pane_Is_A_Bordered_Single_Row_Dots_Metre_Out_Of_The_Tab_Order(PaneControlType type)
    {
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(type);

        Assert.True(metre.Border);
        Assert.Equal(MetreControlStyle.Dots, metre.MetreStyle);
        Assert.False(metre.ShowLegend);
        Assert.Equal(1, metre.Rows);
        Assert.Equal(3, metre.RequiredHeight);
        Assert.Equal(MetreTextPosition.TopLeft, metre.TextPosition);
        Assert.False(metre.TabStop);

        summary.Unload();
    }

    [Theory]
    [InlineData(PaneControlType.Disk)]
    [InlineData(PaneControlType.NetworkReceived)]
    [InlineData(PaneControlType.NetworkSent)]
    public void Byte_Rate_Charts_Have_A_Byte_Rate_Axis(PaneControlType type)
    {
        (SummaryControl2 summary, Control pane) = Shown(type, height: 10);

        Assert.Equal("3G", Assert.IsType<Chart>(pane).CustomYAxisScaleFormatter!(3.0 * 1024 * 1024 * 1024));

        summary.Unload();
    }

    [Fact]
    public void Cpu_Chart_Is_Labelled_With_The_Total_Like_The_Metre()
    {
        RecordingTerminal terminal = new();

        runContext.AppConfig.ShowMetreCpuNumerically = true;

        (SummaryControl2 summary, Control pane) = Shown(PaneControlType.Cpu, height: 10, terminal: terminal);
        Chart chart = Assert.IsType<Chart>(pane);

        Publish(new SystemSnapshot {
            Cpu = new CpuInfo { Metrics = new CpuMetrics { CpuPercentKernelTime = 0.05, CpuPercentUserTime = 0.085 } }
        });

        Assert.Equal("13.5%", chart.LabelSeries);
        Assert.Contains("Cpu 13.5%", terminal.Output);

        summary.Unload();
    }

    [Fact]
    public void Cpu_Metre_Keeps_Its_Two_Series_Across_Reloads()
    {
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(PaneControlType.CpuMetre);

        summary.Unload();
        summary.Load();

        Assert.Equal(2, metre.SeriesCount);

        summary.Unload();
    }

    [Fact]
    public void Cpu_Metre_Shows_Kernel_And_User_Time_And_The_Total_In_The_Border()
    {
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(PaneControlType.CpuMetre);

        Publish(new SystemSnapshot {
            Cpu = new CpuInfo { Metrics = new CpuMetrics { CpuPercentKernelTime = 0.05, CpuPercentUserTime = 0.085 } }
        });

        Assert.Equal(0.05,  metre.Series[0].Value);
        Assert.Equal(0.085, metre.Series[1].Value);
        Assert.Equal("CPU% 13.5%", metre.Text);
        Assert.Equal(runContext.AppConfig.Theme.PerformanceCpuKernel, metre.Series[0].Colour);
        Assert.Equal(runContext.AppConfig.Theme.PerformanceCpuUser, metre.Series[1].Colour);

        summary.Unload();
    }

    // Regression test: the pane was fed MB/s on an axis with no units, so a 3 GB/s peak read "3.3k"
    // above a "2.1 MB/s" label.
    [Fact]
    public void Disk_Chart_Shows_Bytes_On_Its_Axis_And_Label()
    {
        RecordingTerminal terminal = new();

        runContext.AppConfig.ShowMetreDiskNumerically = true;

        (SummaryControl2 summary, Control pane) = Shown(PaneControlType.Disk, height: 10, terminal: terminal);
        Chart chart = Assert.IsType<Chart>(pane);

        Publish(new SystemSnapshot { Disk = new DiskInfo { Metrics = new DiskMetrics { ReadBytesPerSecond = 3.0 * Gibibyte } } });
        terminal.Reset();
        Publish(new SystemSnapshot { Disk = new DiskInfo { Metrics = new DiskMetrics { ReadBytesPerSecond = 2.0 * Mebibyte } } });

        Assert.Equal("2.0 MB/s", chart.LabelSeries);
        Assert.Contains("3G", terminal.Output);
        Assert.Contains("Disk 2.0 MB/s", terminal.Output);

        summary.Unload();
    }

    [Fact]
    public void Disk_Metre_Shows_Active_Time_And_The_Transfer_Rate()
    {
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(PaneControlType.DiskMetre);
        DiskMetrics metrics = new() { PercentActiveTime = 12, ReadBytesPerSecond = 1024 * 1024, WriteBytesPerSecond = 2 * 1024 * 1024 };

        Publish(new SystemSnapshot { Disk = new DiskInfo { Metrics = metrics } });

        Assert.Equal(0.12, metre.Series[0].Value, precision: 9);
        Assert.Equal($"Disk 12.0% · {metrics.ToDiskTransferRate()}", metre.Text);

        summary.Unload();
    }

    [Fact]
    public void Disk_Stack_Pane_Lists_Each_Disk_On_Every_Platform()
    {
        (SummaryControl2 summary, Control pane) = Shown(PaneControlType.DiskStack, width: 120, height: 8);
        DiskStackControl stack = Assert.IsType<DiskStackControl>(pane);

        Assert.False(stack.TabStop);

        Publish(new SystemSnapshot {
            Disk = new DiskInfo {
                Specs   = new DiskSpecs   { Devices = [new DiskDevice { Index = 0, Model = "KINGSTON SKC3000D2048G" }] },
                Metrics = new DiskMetrics { Devices = [new DiskDeviceMetrics { Index = 0, PercentActiveTime = 12.0 }] }
            }
        });

        ListViewItem row = Assert.Single(stack.DiskView.Items.Cast<ListViewItem>());
        Assert.Equal(("Disk 0", "KINGSTON SKC3000D2048G", "012.0%"), (row.SubItems[0].Text, row.SubItems[1].Text, row.SubItems[4].Text));

        summary.Unload();
    }

    [Fact]
    public void Gpu_Memory_Metre_Shows_The_Used_And_Total_Bytes()
    {
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(PaneControlType.GpuMemoryMetre);
        GpuMetrics metrics = new() { TotalGpuMemory = 16 * (long)Gibibyte, AvailableGpuMemory = 12 * (long)Gibibyte };

        Publish(new SystemSnapshot { Gpu = new GpuInfo { Metrics = metrics } });

        Assert.Equal(0.25, metre.Series[0].Value, precision: 9);
        Assert.Equal($"GPU Memory {metrics.ToGpuMemoryRatioFormattedBytes()}", metre.Text);

        summary.Unload();
    }

    [Fact]
    public void Gpu_Metre_Shows_The_Percentage_In_The_High_Load_Colour()
    {
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(PaneControlType.GpuMetre);

        Publish(new SystemSnapshot { Gpu = new GpuInfo { Metrics = new GpuMetrics { GpuPercentTime = 0.9 } } });

        Assert.Equal("GPU% 90.0%", metre.Text);
        Assert.Equal(0.9, metre.Series[0].Value, precision: 9);
        Assert.Equal(runContext.AppConfig.Theme.RangeHighBackground, metre.Series[0].Colour);

        summary.Unload();
    }

    [Fact]
    public void Gpu_Stack_Pane_Lists_Each_Gpu_On_Every_Platform()
    {
        (SummaryControl2 summary, Control pane) = Shown(PaneControlType.GpuStack, width: 90, height: 8);
        GpuStackControl stack = Assert.IsType<GpuStackControl>(pane);

        Assert.False(stack.TabStop);

        Publish(new SystemSnapshot {
            Gpu = new GpuInfo {
                Specs   = new GpuSpecs   { Devices = [new GpuDevice { Index = 0, AdapterLuid = 7, Description = "NVIDIA GeForce RTX 4070 SUPER" }] },
                Metrics = new GpuMetrics { Devices = [new GpuDeviceMetrics { Index = 0, AdapterLuid = 7, GpuPercentTime = 0.232 }] }
            }
        });

        ListViewItem row = Assert.Single(stack.GpuView.Items.Cast<ListViewItem>());
        Assert.Equal(("GPU 0", "NVIDIA GeForce RTX 4070 SUPER", "023.2%"), (row.SubItems[0].Text, row.SubItems[1].Text, row.SubItems[5].Text));

        summary.Unload();
    }

    [Fact]
    public void Memory_Metre_Shows_The_Used_And_Total_Bytes_In_The_Mid_Load_Colour()
    {
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(PaneControlType.MemoryMetre);
        MemoryMetrics metrics = new() { TotalPhysical = 10 * (ulong)Gibibyte, AvailablePhysical = 4 * (ulong)Gibibyte };

        Publish(new SystemSnapshot { Memory = new MemoryInfo { Metrics = metrics } });

        Assert.Equal(0.6, metre.Series[0].Value, precision: 9);
        Assert.Equal($"Memory {metrics.ToMemoryRatioFormattedBytes()}", metre.Text);
        Assert.Equal(runContext.AppConfig.Theme.RangeMidBackground, metre.Series[0].Colour);

        summary.Unload();
    }

    [Fact]
    public void Network_Receive_Metre_Shows_The_Receive_Rate()
    {
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(PaneControlType.NetworkReceiveMetre);
        NetworkMetrics metrics = new() { ReceiveBytesPerSecond = 256 * 1024, SendBytesPerSecond = 1 };

        Publish(new SystemSnapshot { Network = new NetworkInfo { Metrics = metrics } });

        Assert.Equal($"Net Receive {metrics.ToNetworkReceiveRate()}", metre.Text);
        Assert.Equal(1.0, metre.Series[0].Value);

        summary.Unload();
    }

    [Fact]
    public void Network_Send_Metre_Scales_Against_The_Recent_Peak_In_A_Fixed_Colour()
    {
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(PaneControlType.NetworkSendMetre);

        SystemSnapshot Sending(double bytesPerSecond) =>
            new() { Network = new NetworkInfo { Metrics = new NetworkMetrics { SendBytesPerSecond = bytesPerSecond } } };

        Publish(Sending(32 * 1024));
        Assert.Equal(0.5, metre.Series[0].Value, precision: 9);

        Publish(Sending(1024 * 1024));
        Assert.Equal(1.0, metre.Series[0].Value, precision: 9);

        Publish(Sending(256 * 1024));
        Assert.Equal(0.25, metre.Series[0].Value, precision: 9);
        Assert.StartsWith("Net Send ", metre.Text);
        Assert.Equal(runContext.AppConfig.Theme.RangeLowBackground, metre.Series[0].Colour);

        summary.Unload();
    }

    [Fact]
    public void Network_Stack_Pane_Lists_Each_Active_Adapter_On_Every_Platform()
    {
        (SummaryControl2 summary, Control pane) = Shown(PaneControlType.NetworkStack, width: 120, height: 8);
        NetworkStackControl stack = Assert.IsType<NetworkStackControl>(pane);

        Assert.False(stack.TabStop);

        Publish(new SystemSnapshot {
            Network = new NetworkInfo {
                Specs   = new NetworkSpecs   { Devices = [new NetworkDevice { InterfaceLuid = 4, FriendlyName = "Ethernet", ReceiveLinkSpeed = 1_000_000_000 }] },
                Metrics = new NetworkMetrics { Devices = [new NetworkDeviceMetrics { InterfaceLuid = 4, ReceiveBytesPerSecond = 2048 }] }
            }
        });

        ListViewItem row = Assert.Single(stack.NetworkView.Items.Cast<ListViewItem>());
        Assert.Equal(("Ethernet", "1 Gbps", "2.0 KB/s"), (row.SubItems[0].Text, row.SubItems[1].Text, row.SubItems[5].Text));

        summary.Unload();
    }

    private static SystemSnapshot NpuSnapshot(double activeRatio, double? watts) =>
        new() {
            Npu   = new NpuInfo { Metrics = new NpuMetrics { Devices = [new NpuDeviceMetrics { Index = 0, ActiveRatio = activeRatio }] } },
            Power = watts is { } value
                ? new PowerInfo { Metrics = new PowerMetrics { Readings = [new PowerReading { Component = PowerComponent.NeuralEngine, Rail = "Neural Engine", Watts = value }] } }
                : null
        };

    [Fact]
    public void Npu_Chart_Is_Labelled_With_Activity_And_Power_Only_When_Asked()
    {
        (SummaryControl2 summary, Control pane) = Shown(PaneControlType.Npu, height: 10);

#if __APPLE__
        Chart chart = Assert.IsType<Chart>(pane);

        Publish(new SystemSnapshot());
        Assert.Equal(string.Empty, chart.LabelSeries);

        runContext.AppConfig.ShowMetreNpuNumerically = true;
        Publish(NpuSnapshot(0.5, watts: 1.3));
        Assert.Equal($"{0.5:000.0%} · 1.3 W", chart.LabelSeries);

        runContext.AppConfig.ShowMetreNpuNumerically = false;
        Publish(NpuSnapshot(0.5, watts: 1.3));
        Assert.Equal(string.Empty, chart.LabelSeries);
#else
        Assert.IsType<EmptyPaneControl>(pane);
#endif

        summary.Unload();
    }

    [Fact]
    public void Npu_Chart_Metre_And_Fans_Panes_Are_Only_On_MacOS()
    {
        Control Pane(PaneControlType type)
        {
            (SummaryControl2 summary, Control pane) = Shown(type, height: 10);
            summary.Unload();
            return pane;
        }

#if __APPLE__
        Chart chart = Assert.IsType<Chart>(Pane(PaneControlType.Npu));
        Assert.Equal("Npu", chart.Text);
        Assert.False(chart.AutoScale);
        Assert.IsType<MetreControl>(Pane(PaneControlType.NpuMetre));
        Assert.IsType<FansControl>(Pane(PaneControlType.Fans));
#else
        Assert.IsType<EmptyPaneControl>(Pane(PaneControlType.Npu));
        Assert.IsType<EmptyPaneControl>(Pane(PaneControlType.NpuMetre));
        Assert.IsType<EmptyPaneControl>(Pane(PaneControlType.Fans));
#endif
    }

    [Fact]
    public void Npu_Metre_Shows_Activity_And_Any_Power_Reading()
    {
#if __APPLE__
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(PaneControlType.NpuMetre);

        Publish(NpuSnapshot(0.6, watts: 1.4));
        Assert.Equal("NPU% 60.0% · 1.4 W", metre.Text);
        Assert.Equal(0.6, metre.Series[0].Value, precision: 9);

        Publish(NpuSnapshot(0, watts: null));
        Assert.Equal("NPU% 0.0%", metre.Text);
#else
        (SummaryControl2 summary, Control pane) = Shown(PaneControlType.NpuMetre);
        Assert.IsType<EmptyPaneControl>(pane);
#endif

        summary.Unload();
    }

    // ServiceController only raises this from its worker loop.
    private void Publish(SystemSnapshot snapshot)
    {
        MulticastDelegate? handler = (MulticastDelegate?)typeof(ServiceController)
            .GetField("SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(runContext.ServiceController);

        handler?.DynamicInvoke(runContext.ServiceController, new SystemSnapshotEventArgs(snapshot));
    }

    private (SummaryControl2 Summary, Control Pane) Shown(PaneControlType type, int width = 40, int height = 3, ISystemTerminal? terminal = null)
    {
        SummaryControl2 summary = new(
            runContext.ServiceController,
            terminal ?? new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig,
            new LayoutTree<SummaryPane>(new SummaryPane(type))) {
            Width  = width,
            Height = height
        };

        summary.Load();
        summary.Resize();

        return (summary, summary.PaneControls.Values.Single());
    }

    private (SummaryControl2 Summary, MetreControl Metre) ShownMetre(PaneControlType type)
    {
        (SummaryControl2 summary, Control pane) = Shown(type);
        return (summary, Assert.IsType<MetreControl>(pane));
    }

    [Fact]
    public void Thermal_List_Pane_Lists_Each_Component_On_Every_Platform()
    {
        (SummaryControl2 summary, Control pane) = Shown(PaneControlType.ThermalList, width: 60, height: 8);
        ThermalListControl thermals = Assert.IsType<ThermalListControl>(pane);

        Assert.False(thermals.TabStop);

        Publish(new SystemSnapshot {
            Disk    = new DiskInfo    { Specs   = new DiskSpecs { Devices = [new DiskDevice { Index = 0, Model = "KINGSTON SKC3000D2048G" }] } },
            Thermal = new ThermalInfo { Metrics = new ThermalMetrics {
                Sensors = [new ThermalSensor { Component = ThermalComponent.Disk, ComponentId = "0", SensorName = "Composite", Celsius = 38 }]
            } }
        });

        ListViewItem row = Assert.Single(thermals.ThermalView.Items.Cast<ListViewItem>());
        Assert.Equal(("Disk 0", "KINGSTON SKC3000D2048G", "38°C"), (row.SubItems[0].Text, row.SubItems[1].Text, row.SubItems[2].Text));

        summary.Unload();
    }

    [Fact]
    public void Virtual_Memory_Metre_Is_Labelled_For_The_Platform()
    {
        (SummaryControl2 summary, MetreControl metre) = ShownMetre(PaneControlType.VirtualMemoryMetre);
        MemoryMetrics metrics = new() { TotalPageFile = 4 * (ulong)Gibibyte, AvailablePageFile = 3 * (ulong)Gibibyte };

        Publish(new SystemSnapshot { Memory = new MemoryInfo { Metrics = metrics } });

#if __APPLE__
        Assert.Equal($"Swap {metrics.ToPageFileMemoryRatioFormattedBytes()}", metre.Text);
#else
        Assert.Equal($"Virtual {metrics.ToPageFileMemoryRatioFormattedBytes()}", metre.Text);
#endif
        Assert.Equal(0.25, metre.Series[0].Value, precision: 9);

        summary.Unload();
    }
}
