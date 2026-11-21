#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Native;
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Thermal.Providers.Windows;

// NVIDIA GPUs via NVAPI's GetThermalSettings.
internal sealed class NvApiThermalProvider(Func<IReadOnlyList<GpuDevice>> getGpus) : IReadingProvider<ThermalSensor>
{
    private bool initialised;

    public string Diagnostic { get; private set; } = string.Empty;

    public void Dispose()
    {
        if (!initialised) {
            return;
        }

        try {
            NvApiLibrary.Unload();
        }
        catch (DllNotFoundException) {
            // Driver dropped.
        }

        initialised = false;
    }

    public string Name => "NVAPI";

    public IEnumerable<ThermalSensor> Read()
    {
        if (!initialised) {
            yield break;
        }

        nint[] handles = new nint[NvApi.NvApiMaxPhysicalGpus];
        int count;

        try {
            count = NvApiLibrary.EnumPhysicalGpus(handles);
        }
        catch (DllNotFoundException) {
            initialised = false;
            yield break;
        }

        GpuDeviceMatcher matcher = new(getGpus());

        for (int i = 0; i < count; i++) {
            if (!NvApiLibrary.TryGetPciIds(handles[i], out uint vendorId, out uint deviceId)) {
                continue;
            }

            GpuDevice? gpu = matcher.Match(vendorId, deviceId);

            string componentId = gpu?.AdapterLuid.ToString() ?? $"nvapi{i}";

            foreach (NvApiLibrary.ThermalReading reading in NvApiLibrary.GetThermalSettings(handles[i])) {
                yield return new ThermalSensor {
                    Component   = ThermalComponent.Gpu,
                    ComponentId = componentId,
                    SensorName  = TargetName(reading.Target),
                    Celsius     = reading.Celsius,
                    Source      = ThermalSource.NvApi
                };
            }
        }
    }

    private static string TargetName(int target) => target switch {
        NvApi.TargetGpu         => "GPU",
        NvApi.TargetMemory      => "Memory",
        NvApi.TargetPowerSupply => "Power Supply",
        NvApi.TargetBoard       => "Board",
                              _ => "Sensor"
    };

    public bool TryInitialise()
    {
        initialised = NvApiLibrary.Initialize(out string diagnostic);
        Diagnostic  = diagnostic;

        return initialised;
    }
}
#endif
