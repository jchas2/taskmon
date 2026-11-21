#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Native;
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Power.Providers.Windows;

// NVidia GPUs via NVML.
internal sealed class NvmlPowerProvider(Func<IReadOnlyList<GpuDevice>> getGpus) : IReadingProvider<PowerReading>
{
    private bool initialised;

    public string Diagnostic { get; private set; } = string.Empty;

    public void Dispose()
    {
        if (initialised) {
            NvmlLibrary.Shutdown();
            initialised = false;
        }
    }

    public string Name => "NVML";

    public IEnumerable<PowerReading> Read()
    {
        if (!initialised) {
            yield break;
        }

        GpuDeviceMatcher matcher = new(getGpus());
        int              count   = NvmlLibrary.DeviceCount();

        for (int i = 0; i < count; i++) {
            if (!NvmlLibrary.TryGetHandle(i, out nint handle) || !NvmlLibrary.TryGetPowerWatts(handle, out double watts)) {
                continue;
            }

            string id = $"nvml{i}";

            if (NvmlLibrary.TryGetPciIds(handle, out uint vendorId, out uint deviceId) &&
                matcher.Match(vendorId, deviceId) is { } gpu) {

                id = gpu.AdapterLuid.ToString();
            }

            yield return new PowerReading {
                Component   = PowerComponent.Gpu,
                ComponentId = id,
                Rail        = PowerRails.Gpu,
                Watts       = watts,
                Source      = PowerSource.Nvml
            };
        }
    }

    public bool TryInitialise()
    {
        initialised = NvmlLibrary.Initialize(out string diagnostic);
        Diagnostic  = diagnostic;

        return initialised;
    }
}
#endif
