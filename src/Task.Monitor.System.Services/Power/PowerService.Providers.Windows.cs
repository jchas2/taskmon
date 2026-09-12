#if __WIN32__
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Power;

public partial class PowerService
{
    private partial IEnumerable<IPowerProvider> CreateProviders()
    {
        IReadOnlyList<DiskDevice> Disks() => GetLatest<DiskInfo>()?.Specs.Devices ?? [];
        IReadOnlyList<GpuDevice> Gpus() => GetLatest<GpuInfo>()?.Specs.Devices ?? [];

        return [
            new NvmlPowerProvider(Gpus),
            new AdlPowerProvider(Gpus),
            new PowerMeterPdhProvider(),
            new BatteryPowerProvider(),
            new NvmeRatedPowerProvider(Disks),
        ];
    }
}
#endif
