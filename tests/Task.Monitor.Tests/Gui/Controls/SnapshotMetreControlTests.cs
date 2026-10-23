using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Gui.Controls.Disk;
using Task.Monitor.Gui.Controls.Gpu;
using Task.Monitor.Gui.Controls.Memory;
using Task.Monitor.Gui.Controls.Network;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class SnapshotMetreControlTests
{
    private const ulong Gigabyte = 1024UL * 1024 * 1024;

    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public SnapshotMetreControlTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private T Create<T>(Func<ServiceController, ForwardingTerminal, AppConfig, T> create) where T : SnapshotMetreControl
    {
        T ctrl = create(runContext.ServiceController, new ForwardingTerminal(runContext.Terminal), runContext.AppConfig);

        ctrl.Width  = 40;
        ctrl.Height = 3;
        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    [Fact]
    public void Disk_Shows_Active_Time_And_The_Transfer_Rate()
    {
        DiskMetreControl ctrl = Create((s, t, a) => new DiskMetreControl(s, t, a));
        DiskMetrics metrics = new() { PercentActiveTime = 12, ReadBytesPerSecond = 1024 * 1024, WriteBytesPerSecond = 2 * 1024 * 1024 };

        ctrl.Sample(new SystemSnapshot { Disk = new DiskInfo { Metrics = metrics } });

        Assert.Equal(0.12, ctrl.Metre.Series[0].Value, precision: 9);
        Assert.Equal($"Disk 12.0% · {metrics.ToDiskTransferRate()}", ctrl.Metre.Text);

        ctrl.Unload();
    }

    [Fact]
    public void GpuMemory_Shows_The_Used_And_Total_Bytes()
    {
        GpuMemoryMetreControl ctrl = Create((s, t, a) => new GpuMemoryMetreControl(s, t, a));
        GpuMetrics metrics = new() { TotalGpuMemory = 16 * (long)Gigabyte, AvailableGpuMemory = 12 * (long)Gigabyte };

        ctrl.Sample(new SystemSnapshot { Gpu = new GpuInfo { Metrics = metrics } });

        Assert.Equal(0.25, ctrl.Metre.Series[0].Value, precision: 9);
        Assert.Equal($"GPU Memory {metrics.ToGpuMemoryRatioFormattedBytes()}", ctrl.Metre.Text);

        ctrl.Unload();
    }

    [Fact]
    public void Gpu_Shows_The_Percentage_In_The_High_Load_Colour()
    {
        GpuMetreControl ctrl = Create((s, t, a) => new GpuMetreControl(s, t, a));

        ctrl.Sample(new SystemSnapshot { Gpu = new GpuInfo { Metrics = new GpuMetrics { GpuPercentTime = 0.9 } } });

        Assert.Equal("GPU% 90.0%", ctrl.Metre.Text);
        Assert.Equal(0.9, ctrl.Metre.Series[0].Value, precision: 9);
        Assert.Equal(runContext.AppConfig.Theme.RangeHighBackground, ctrl.Metre.Series[0].Colour);

        ctrl.Unload();
    }

    [Fact]
    public void Ignores_A_Snapshot_Without_Its_Data()
    {
        MemoryMetreControl ctrl = Create((s, t, a) => new MemoryMetreControl(s, t, a));

        ctrl.Sample(new SystemSnapshot());

        Assert.Equal("Memory", ctrl.Metre.Text);
        Assert.Equal(0.0, ctrl.Metre.Series[0].Value);

        ctrl.Unload();
    }

    [Fact]
    public void Memory_Shows_The_Used_And_Total_Bytes_In_The_Mid_Load_Colour()
    {
        MemoryMetreControl ctrl = Create((s, t, a) => new MemoryMetreControl(s, t, a));
        MemoryMetrics metrics = new() { TotalPhysical = 10 * Gigabyte, AvailablePhysical = 4 * Gigabyte };

        ctrl.Sample(new SystemSnapshot { Memory = new MemoryInfo { Metrics = metrics } });

        Assert.Equal(0.6, ctrl.Metre.Series[0].Value, precision: 9);
        Assert.Equal($"Memory {metrics.ToMemoryRatioFormattedBytes()}", ctrl.Metre.Text);
        Assert.Equal(runContext.AppConfig.Theme.RangeMidBackground, ctrl.Metre.Series[0].Colour);

        ctrl.Unload();
    }

    [Fact]
    public void NetworkReceive_Shows_The_Receive_Rate()
    {
        NetworkReceiveMetreControl ctrl = Create((s, t, a) => new NetworkReceiveMetreControl(s, t, a));
        NetworkMetrics metrics = new() { ReceiveBytesPerSecond = 256 * 1024, SendBytesPerSecond = 1 };

        ctrl.Sample(new SystemSnapshot { Network = new NetworkInfo { Metrics = metrics } });

        Assert.Equal($"Net Receive {metrics.ToNetworkReceiveRate()}", ctrl.Metre.Text);
        Assert.Equal(1.0, ctrl.Metre.Series[0].Value);

        ctrl.Unload();
    }

    [Fact]
    public void NetworkSend_Scales_Against_The_Recent_Peak_In_A_Fixed_Colour()
    {
        NetworkSendMetreControl ctrl = Create((s, t, a) => new NetworkSendMetreControl(s, t, a));

        SystemSnapshot Sending(double bytesPerSecond) =>
            new() { Network = new NetworkInfo { Metrics = new NetworkMetrics { SendBytesPerSecond = bytesPerSecond } } };

        ctrl.Sample(Sending(32 * 1024));
        Assert.Equal(0.5, ctrl.Metre.Series[0].Value, precision: 9);

        ctrl.Sample(Sending(1024 * 1024));
        Assert.Equal(1.0, ctrl.Metre.Series[0].Value, precision: 9);

        ctrl.Sample(Sending(256 * 1024));
        Assert.Equal(0.25, ctrl.Metre.Series[0].Value, precision: 9);
        Assert.StartsWith("Net Send ", ctrl.Metre.Text);
        Assert.Equal(runContext.AppConfig.Theme.RangeLowBackground, ctrl.Metre.Series[0].Colour);

        ctrl.Unload();
    }

    [Fact]
    public void VirtualMemory_Is_Labelled_For_The_Platform()
    {
        VirtualMemoryMetreControl ctrl = Create((s, t, a) => new VirtualMemoryMetreControl(s, t, a));
        MemoryMetrics metrics = new() { TotalPageFile = 4 * Gigabyte, AvailablePageFile = 3 * Gigabyte };

        ctrl.Sample(new SystemSnapshot { Memory = new MemoryInfo { Metrics = metrics } });

#if __APPLE__
        Assert.Equal($"Swap {metrics.ToPageFileMemoryRatioFormattedBytes()}", ctrl.Metre.Text);
#else
        Assert.Equal($"Virtual {metrics.ToPageFileMemoryRatioFormattedBytes()}", ctrl.Metre.Text);
#endif
        Assert.Equal(0.25, ctrl.Metre.Series[0].Value, precision: 9);

        ctrl.Unload();
    }
}
