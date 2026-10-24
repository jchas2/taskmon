#if __APPLE__
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Thermal.Providers.MacOS;

internal sealed class SmcThermalProvider(SmcThermalSampler sampler, Func<IReadOnlyList<GpuDevice>> getGpus) : IThermalProvider
{
    private const string HottestSensor = "Hottest";

    public void Dispose() { }

    public string Name => "SMC";

    public IEnumerable<ThermalSensor> Read()
    {
        string gpuId = getGpus().FirstOrDefault()?.AdapterLuid.ToString() ?? string.Empty;

        return SmcThermalKeys.Hottest(sampler.ReadTemperatures()).Select(reading => new ThermalSensor {
            Component   = reading.Group.Component,
            ComponentId = reading.Group.Component == ThermalComponent.Gpu ? gpuId : reading.Group.Cluster,
            SensorName  = HottestSensor,
            Celsius     = reading.Celsius,
            Source      = ThermalSource.Smc
        });
    }

    public bool TryInitialise() => sampler.EnsureOpen() && sampler.HasTemperatures;
}
#endif
