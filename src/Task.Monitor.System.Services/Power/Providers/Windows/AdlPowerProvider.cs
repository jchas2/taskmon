#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Power.Providers.Windows;

// AMD GPUs via ADL.
internal sealed class AdlPowerProvider(Func<IReadOnlyList<GpuDevice>> getGpus) : IPowerProvider
{
    private nint context;

    public string Name => "ADL power";

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

    public IEnumerable<PowerReading> Read()
    {
        if (context == nint.Zero) {
            yield break;
        }

        IReadOnlyList<GpuDevice> gpus = getGpus();
        HashSet<long> assigned = new();

        foreach (Adl.Adapter adapter in Adl.GetAdapters(context)) {
            if (!Adl.GetPowerWatts(context, adapter.AdapterIndex, out double watts)) {
                continue;
            }

            GpuDevice? gpu = gpus.FirstOrDefault(candidate =>
                candidate.DeviceId == adapter.DeviceId && assigned.Add(candidate.AdapterLuid));

            yield return new PowerReading {
                Component = PowerComponent.Gpu,
                ComponentId = gpu?.AdapterLuid.ToString() ?? $"adl{adapter.AdapterIndex}",
                Rail = "GPU",
                Watts = watts,
                Source = PowerSource.Adl
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
