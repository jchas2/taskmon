#if __APPLE__
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Thermal.Providers.MacOS;

internal sealed class SmcThermalProvider(SmcThermalSampler sampler, Func<IReadOnlyList<GpuDevice>> getGpus) : IThermalProvider
{
    private const string HottestSensor = "Hottest";

    public string Diagnostic { get; private set; } = string.Empty;

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

    public bool TryInitialise()
    {
        if (!sampler.EnsureOpen()) {
            Diagnostic = sampler.OpenDiagnostic;
            return false;
        }

        if (!sampler.HasTemperatures) {
            Diagnostic = "NOT FOUND, the SMC has no temperature keys this build knows";
            return false;
        }

        Diagnostic = "OK";
        return true;
    }
}
#endif
