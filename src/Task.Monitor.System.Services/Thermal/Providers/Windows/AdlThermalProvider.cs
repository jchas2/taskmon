#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Thermal.Providers.Windows;

// AMD GPUs via ADL's Overdrive temperature calls. 
internal sealed class AdlThermalProvider(Func<IReadOnlyList<GpuDevice>> getGpus) : IThermalProvider
{
    private nint context;
    
    public string Name => "ADL";

    public bool TryInitialise()
    {
        context = nint.Zero;

        try {
            Adl.TryCreate(out context);
        }
        catch (DllNotFoundException) {
            context = nint.Zero;
        }

        return context != nint.Zero;
    }

    public IEnumerable<ThermalSensor> Read()
    {
        if (context == nint.Zero) {
            yield break;
        }

        IReadOnlyList<GpuDevice> gpus = getGpus();
        HashSet<long> assigned = new();

        foreach (Adl.Adapter adapter in Adl.GetAdapters(context)) {
            if (!Adl.GetTemperatureCelsius(context, adapter.AdapterIndex, out double celsius)) {
                continue;
            }

            GpuDevice? gpu = gpus.FirstOrDefault(candidate =>
                candidate.DeviceId == adapter.DeviceId && assigned.Add(candidate.AdapterLuid));

            yield return new ThermalSensor {
                Component   = ThermalComponent.Gpu,
                ComponentId = gpu?.AdapterLuid.ToString() ?? $"adl{adapter.AdapterIndex}",
                SensorName  = "GPU",
                Celsius     = celsius,
                Source      = ThermalSource.Adl
            };
        }
    }

    public void Dispose()
    {
        if (context != nint.Zero) {
            Adl.Destroy(context);
            context = nint.Zero;
        }
    }
}
#endif
