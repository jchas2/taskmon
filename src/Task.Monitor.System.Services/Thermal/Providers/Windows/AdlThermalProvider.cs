#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Thermal.Providers.Windows;

// AMD GPUs via ADL's Overdrive temperature calls.
internal sealed class AdlThermalProvider(Func<IReadOnlyList<GpuDevice>> getGpus) : IThermalProvider
{
    private readonly AdlAdapters adapters = new();

    public void Dispose() => adapters.Dispose();

    public string Name => "ADL";

    public IEnumerable<ThermalSensor> Read() =>
        adapters.Read(getGpus(), Adl.GetTemperatureCelsius).Select(reading => new ThermalSensor {
            Component   = ThermalComponent.Gpu,
            ComponentId = reading.ComponentId,
            SensorName  = "GPU",
            Celsius     = reading.Value,
            Source      = ThermalSource.Adl
        });

    public bool TryInitialise() => adapters.TryOpen();
}
#endif
