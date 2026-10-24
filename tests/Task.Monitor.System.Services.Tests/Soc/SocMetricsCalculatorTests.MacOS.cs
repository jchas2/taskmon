#if __APPLE__
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Task.Monitor.System.Services.Npu;
using Task.Monitor.System.Services.Soc;
using Xunit.Abstractions;

namespace Task.Monitor.System.Services.Tests.Soc;

public sealed class SocMetricsCalculatorTests(ITestOutputHelper output)
{
    private const int MinimumDelay = 500;

    // The M3 Pro GPU table as read from pmgr: OFF, then P1..P13.
    private static readonly double[] M3ProGpuMhz = [0, 338, 618, 796, 832, 924, 952, 1056, 1064, 1182, 1182, 1312, 1242, 1380];

    [Fact]
    public void BuildNpuInfo_Publishes_The_Specs_Even_Without_A_Reading()
    {
        NpuDevice device = new() { Index = 0, Cores = 16 };

        NpuInfo withReading    = SocService.BuildNpuInfo(device, 0.25);
        NpuInfo withoutReading = SocService.BuildNpuInfo(device, null);

        Assert.Same(device, Assert.Single(withReading.Specs.Devices));
        Assert.Equal(0.25, Assert.Single(withReading.Metrics.Devices).ActiveRatio);
        Assert.Single(withoutReading.Specs.Devices);
        Assert.Empty(withoutReading.Metrics.Devices);
    }

    [Fact]
    public void Calculate_Leaves_Unreported_Values_Null()
    {
        SocMetrics metrics = SocMetricsCalculator.Calculate(
            [State("GPU Stats", "GPU Performance States", ("OFF", 100))], []);

        Assert.Null(metrics.GpuFrequencyMhz);
        Assert.Null(metrics.GpuMaxFrequencyMhz);
        Assert.Null(metrics.GpuThermalLimitRatio);
    }

    [Fact]
    public void Calculate_Reads_Each_Channel()
    {
        IOReportChannelSample[] channels = [
            State("GPU Stats", "GPU Performance States", ("OFF", 50), ("P1", 25), ("P13", 25)),
            State("GPU Stats", "CLTM-induced GPU Performance States", ("NO_CLTM", 75), ("P5", 25)),
            State("GPU Stats", "GPU Power Controller States", ("IDLE_OFF", 500), ("PERF", 60), ("POWER", 30), ("PPMLIMIT", 10)),
            State("ANE", "IOP State", ("Off", 40), ("Running", 60)),
        ];

        SocMetrics metrics = SocMetricsCalculator.Calculate(channels, M3ProGpuMhz);

        Assert.Equal((338 + 1380) / 2.0, metrics.GpuFrequencyMhz);
        Assert.Equal(1380, metrics.GpuMaxFrequencyMhz);
        Assert.Equal(0.25, metrics.GpuThermalLimitRatio);
        Assert.Equal(0.4, metrics.GpuPowerLimitRatio!.Value, precision: 9);
        Assert.Equal(0.6, SocMetricsCalculator.NeuralEngineActiveRatio(channels));
    }

    [Fact]
    public void NeuralEngineActiveRatio_Is_Null_Without_The_Ane_Channel()
    {
        Assert.Null(SocMetricsCalculator.NeuralEngineActiveRatio([State("GPU Stats", "GPU Performance States", ("OFF", 100))]));
    }

    [Fact]
    public void NeuralEngineParser_Reads_The_Driver_Properties()
    {
        Dictionary<string, object?> properties = new() {
            ["ANEDevicePropertyNumANECores"]                 = 16L,
            ["ANEDevicePropertyTypeANEArchitectureTypeStr"]  = "h15g",
            ["ANEDevicePropertyANEVersion"]                  = 144L,
            ["ANEDevicePropertyANEMinorVersion"]             = 17L,
        };

        NpuDevice device = NeuralEngineParser.Parse(properties)!;

        Assert.Equal(0, device.Index);
        Assert.Equal("Apple Neural Engine", device.Description);
        Assert.Equal("Apple", device.Vendor);
        Assert.Equal(16, device.Cores);
        Assert.Equal("h15g", device.Architecture);
        Assert.Equal("144.17", device.Version);
    }

    [Fact]
    public void NeuralEngineParser_Rejects_A_Device_Without_Cores()
    {
        Assert.Null(NeuralEngineParser.Parse(new Dictionary<string, object?> { ["ANEDevicePropertyTypeANEArchitectureTypeStr"] = "h15g" }));
    }

    [Fact]
    public void ParseFrequencyTable_Reads_Hertz_Entries_As_Megahertz()
    {
        Assert.Equal([0, 338, 1380], SocMetricsCalculator.ParseFrequencyTable(Table(0, 338_000_000, 1_380_000_000)));
    }

    [Fact]
    public void ParseFrequencyTable_Reads_Kilohertz_Entries_As_Megahertz()
    {
        Assert.Equal([0, 338, 1578], SocMetricsCalculator.ParseFrequencyTable(Table(0, 338_000, 1_578_000)));
    }

    [Fact]
    public void Share_Ignores_Excluded_States()
    {
        Assert.Equal(0.5, SocMetricsCalculator.Share(
            [("IDLE_OFF", 900), ("PERF", 50), ("POWER", 50)],
            state => state == "POWER",
            state => state == "IDLE_OFF"));
    }

    [SkippableFact]
    public void SocService_Publishes_Gpu_Clocks_And_The_Npu()
    {
        Skip.IfNot(RuntimeInformation.ProcessArchitecture == Architecture.Arm64, "Needs Apple Silicon");

        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(() => new SocService { Delay = MinimumDelay });
        controller.Start();

        SystemSnapshot? latest = snapshots.WaitForUpdates(snapshot => snapshot.Npu, count: 2);
        controller.Stop();

        SocMetrics metrics = latest!.Soc!.Metrics;
        NpuDevice  npu     = Assert.Single(latest.Npu!.Specs.Devices);
        double     active  = Assert.Single(latest.Npu.Metrics.Devices).ActiveRatio;

        output.WriteLine($"gpu {metrics.GpuFrequencyMhz}/{metrics.GpuMaxFrequencyMhz} MHz, " +
                         $"thermal {metrics.GpuThermalLimitRatio}, power {metrics.GpuPowerLimitRatio}, " +
                         $"npu {npu.Cores} cores {npu.Architecture} {npu.Version}, active {active}");

        Assert.InRange(metrics.GpuMaxFrequencyMhz ?? 0, 300, 5000);
        Assert.True(npu.Cores > 0);
        Assert.InRange(active, 0, 1);
    }

    private static IOReportChannelSample State(string group, string subGroup, params (string Name, long Residency)[] states) =>
        new(group, subGroup, subGroup, 0, states);

    private static byte[] Table(params uint[] frequencies)
    {
        byte[] data = new byte[frequencies.Length * 8];

        for (int i = 0; i < frequencies.Length; i++) {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i * 8), frequencies[i]);
        }

        return data;
    }
}
#endif
