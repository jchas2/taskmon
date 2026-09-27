#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Power.Providers.Windows;

// NVidia GPUs via NVML.
internal sealed class NvmlPowerProvider(Func<IReadOnlyList<GpuDevice>> getGpus) : IPowerProvider
{
    private bool initialised;

    public string Name => "NVML";

    public bool TryInitialise()
    {
        try {
            initialised = Nvml.Initialize();
        }
        catch (DllNotFoundException) {
            initialised = false;
        }

        return initialised;
    }

    public IEnumerable<PowerReading> Read()
    {
        if (!initialised) {
            yield break;
        }

        IReadOnlyList<GpuDevice> gpus = getGpus();
        HashSet<long> assigned = new();
        int count = Nvml.DeviceCount();

        for (int i = 0; i < count; i++) {
            if (!Nvml.TryGetHandle(i, out nint handle) || !Nvml.TryGetPowerWatts(handle, out double watts)) {
                continue;
            }

            string id = $"nvml{i}";

            if (Nvml.TryGetPciIds(handle, out uint vendorId, out uint deviceId)) {
                GpuDevice? gpu = gpus.FirstOrDefault(candidate =>
                    candidate.VendorId == vendorId
                    && candidate.DeviceId == deviceId
                    && assigned.Add(candidate.AdapterLuid));

                if (gpu != null) {
                    id = gpu.AdapterLuid.ToString();
                }
            }

            yield return new PowerReading {
                Component = PowerComponent.Gpu,
                ComponentId = id,
                Rail = "GPU",
                Watts = watts,
                Source = PowerSource.Nvml
            };
        }
    }

    public void Dispose()
    {
        if (initialised) {
            Nvml.Shutdown();
            initialised = false;
        }
    }
}
#endif
