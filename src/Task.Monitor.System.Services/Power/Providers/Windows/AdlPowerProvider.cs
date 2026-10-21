#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Power.Providers.Windows;

// AMD GPUs via ADL.
internal sealed class AdlPowerProvider(Func<IReadOnlyList<GpuDevice>> getGpus) : IPowerProvider
{
    private readonly AdlAdapters adapters = new();

    public void Dispose() => adapters.Dispose();

    public string Name => "ADL power";

    public IEnumerable<PowerReading> Read() =>
        adapters.Read(getGpus(), Adl.GetPowerWatts).Select(reading => new PowerReading {
            Component   = PowerComponent.Gpu,
            ComponentId = reading.ComponentId,
            Rail        = "GPU",
            Watts       = reading.Value,
            Source      = PowerSource.Adl
        });

    public bool TryInitialise() => adapters.TryOpen();
}
#endif
