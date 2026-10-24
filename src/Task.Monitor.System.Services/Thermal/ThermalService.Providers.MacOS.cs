#if __APPLE__
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Thermal.Providers.MacOS;

namespace Task.Monitor.System.Services.Thermal;

public partial class ThermalService
{
    private readonly SmcThermalSampler      smcSampler      = new();
    private readonly ThermalPressureMonitor pressureMonitor = new();

    private partial IEnumerable<IThermalProvider> CreateProviders()
    {
        IReadOnlyList<DiskDevice> Disks() => GetLatest<DiskInfo>()?.Specs.Devices ?? [];
        IReadOnlyList<GpuDevice> Gpus()   => GetLatest<GpuInfo>()?.Specs.Devices ?? [];

        return [
            new SmcThermalProvider(smcSampler, Gpus),
            new HidThermalProvider(Disks),
        ];
    }

    partial void OnStopPlatform()
    {
        smcSampler.Dispose();
        pressureMonitor.Dispose();
    }

    partial void ReadPlatform(ThermalMetrics metrics)
    {
        if (smcSampler.EnsureOpen()) {
            metrics.Fans = smcSampler.ReadFans();
        }

        metrics.Pressure = pressureMonitor.Read();
    }
}
#endif
