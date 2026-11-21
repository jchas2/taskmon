#if __APPLE__
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Power.Providers.MacOS;

namespace Task.Monitor.System.Services.Power;

public partial class PowerService
{
    private readonly IOReportSampler energySampler = new(nameof(PowerService), ("Energy Model", null));

    private partial IEnumerable<IReadingProvider<PowerReading>> CreateProviders()
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
