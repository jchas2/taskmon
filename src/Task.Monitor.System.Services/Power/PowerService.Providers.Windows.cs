#if __WIN32__
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Power.Providers.Windows;

namespace Task.Monitor.System.Services.Power;

public partial class PowerService
{
    private partial IEnumerable<IPowerProvider> CreateProviders()
    {
        IReadOnlyList<DiskDevice> Disks() => GetLatest<DiskInfo>()?.Specs.Devices ?? [];
        IReadOnlyList<GpuDevice> Gpus() => GetLatest<GpuInfo>()?.Specs.Devices ?? [];

        return [
            new NvmlPowerProvider(Gpus),        // Nvidia GPUs.
            new AdlPowerProvider(Gpus),         // AMD GPUs.
            new PowerMeterPdhProvider(),        
            new BatteryPowerProvider(),
            new NvmeRatedPowerProvider(Disks),  // Non-volatile Memory Express interface.
        ];
    }
}
#endif
