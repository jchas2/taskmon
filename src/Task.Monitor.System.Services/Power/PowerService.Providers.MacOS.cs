#if __APPLE__
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Power.Providers.MacOS;

namespace Task.Monitor.System.Services.Power;

public partial class PowerService
{
    private readonly IOReportEnergySampler energySampler = new();

    private partial IEnumerable<IPowerProvider> CreateProviders()
    {
        IReadOnlyList<GpuDevice> Gpus() => GetLatest<GpuInfo>()?.Specs.Devices ?? [];

        return [
            new IOReportEnergyPowerProvider(energySampler, Gpus),
            new SmartBatteryPowerProvider(),
            new SmcPowerProvider(),
        ];
    }

    partial void OnStopPlatform() => energySampler.Dispose();
}
#endif
