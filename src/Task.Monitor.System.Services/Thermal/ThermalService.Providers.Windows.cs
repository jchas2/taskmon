#if __WIN32__
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Thermal;

public partial class ThermalService
{
    private partial IEnumerable<IThermalProvider> CreateProviders()
    {
        IReadOnlyList<DiskDevice> Disks() => GetLatest<DiskInfo>()?.Specs.Devices ?? [];
        IReadOnlyList<GpuDevice> Gpus() => GetLatest<GpuInfo>()?.Specs.Devices ?? [];

        return [
            new NvmeDiskThermalProvider(Disks),
            new AtaDiskThermalProvider(Disks),
            new NvApiThermalProvider(Gpus),
            new AdlThermalProvider(Gpus),
            new AcpiThermalZoneProvider(),
        ];
    }
}
#endif
