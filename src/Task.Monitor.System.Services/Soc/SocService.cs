#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Npu;

namespace Task.Monitor.System.Services.Soc;

// Apple Silicon GPU clocks, GPU limits and Neural Engine activity, from IOReport.
public sealed class SocService : WorkerService
{
    private const string NeuralEngineDriver  = "H11ANEIn";
    private const string NeuralEngineDetails = "DeviceProperties";
    private const string PowerManagerService = "pmgr";
    private const string IOServicePlane      = "IOService";

    private static readonly string[] GpuFrequencyKeys = ["voltage-states9-sram", "voltage-states9"];

    private readonly IOReportSampler sampler = new(
        ("GPU Stats", "GPU Performance States"),
        ("GPU Stats", "CLTM-induced GPU Performance States"),
        ("GPU Stats", "GPU Power Controller States"),
        ("ANE",       "IOP State"));

    private IReadOnlyList<double> gpuFrequencies = [];
    private NpuDevice?            neuralEngine;

    internal static NpuInfo BuildNpuInfo(NpuDevice device, double? activeRatio)
    {
        NpuInfo info = new();
        info.Specs.Devices.Add(device);

        if (activeRatio is { } ratio) {
            info.Metrics.Devices.Add(new NpuDeviceMetrics { Index = device.Index, ActiveRatio = ratio });
        }

        return info;
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (!sampler.EnsureSubscribed() || sampler.Sample() is not { } channels) {
            return;
        }

        Publish(new SocInfo { Metrics = SocMetricsCalculator.Calculate(channels, gpuFrequencies) });

        if (neuralEngine is not null) {
            Publish(BuildNpuInfo(neuralEngine, SocMetricsCalculator.NeuralEngineActiveRatio(channels)));
        }
    }

    protected override void OnStart()
    {
        gpuFrequencies = ReadGpuFrequencies();
        neuralEngine   = ReadNeuralEngine();
    }

    protected override void OnStop() => sampler.Dispose();

    private static IReadOnlyList<double> ReadGpuFrequencies()
    {
        using IOObjectScope pmgr = new(IOKit.IOServiceGetMatchingService(0, IOKit.IOServiceNameMatching(PowerManagerService)));

        if (pmgr.IsNull) {
            return [];
        }

        foreach (string key in GpuFrequencyKeys) {
            using CFScope name  = new(CoreFoundation.CFStringCreate(key));
            using CFScope value = new(IOKit.IORegistryEntrySearchCFProperty(pmgr, IOServicePlane, name, nint.Zero, 0));

            if (!value.IsNull && PropertyList.ToManaged(value) is byte[] data) {
                return SocMetricsCalculator.ParseFrequencyTable(data);
            }
        }

        return [];
    }

    private static NpuDevice? ReadNeuralEngine()
    {
        using IOObjectScope driver = new(IOKit.IOServiceGetMatchingService(0, IOKit.IOServiceMatching(NeuralEngineDriver)));

        if (driver.IsNull) {
            return null;
        }

        using CFScope name  = new(CoreFoundation.CFStringCreate(NeuralEngineDetails));
        using CFScope value = new(IOKit.IORegistryEntrySearchCFProperty(driver, IOServicePlane, name, nint.Zero, 0));

        return !value.IsNull && PropertyList.ToManaged(value) is Dictionary<string, object?> properties
            ? NeuralEngineParser.Parse(properties)
            : null;
    }
}
#endif
