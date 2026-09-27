#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Thermal.Providers.Windows;

// NVIDIA GPUs via NVAPI's GetThermalSettings.
internal sealed class NvApiThermalProvider(Func<IReadOnlyList<GpuDevice>> getGpus) : IThermalProvider
{
    private bool initialised;

    public string Name => "NVAPI";

    public bool TryInitialise()
    {
        try {
            initialised = NvApi.Initialize();
        }
        catch (DllNotFoundException) {
            initialised = false;
        }
        catch (EntryPointNotFoundException) {
            initialised = false;
        }

        return initialised;
    }

    public IEnumerable<ThermalSensor> Read()
    {
        if (!initialised) {
            yield break;
        }

        nint[] handles = new nint[NvApi.NvApiMaxPhysicalGpus];
        int count;

        try {
            count = NvApi.EnumPhysicalGpus(handles);
        }
        catch (DllNotFoundException) {
            initialised = false;
            yield break;
        }

        IReadOnlyList<GpuDevice> gpus = getGpus();
        Dictionary<long, int> assignedByLuid = new();

        for (int i = 0; i < count; i++) {
            if (!NvApi.TryGetPciIds(handles[i], out uint vendorId, out uint deviceId)) {
                continue;
            }

            GpuDevice? gpu = MatchGpu(
                gpus, 
                vendorId, 
                deviceId, 
                assignedByLuid);
            
            string componentId = gpu?.AdapterLuid.ToString() ?? $"nvapi{i}";

            foreach (NvApi.ThermalReading reading in NvApi.GetThermalSettings(handles[i])) {
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

    private static GpuDevice? MatchGpu(
        IReadOnlyList<GpuDevice> gpus, 
        uint vendorId, 
        uint deviceId, 
        Dictionary<long, int> assigned)
    {
        // Match on PCI ids; if several identical cards match, hand out each GpuDevice once.
        foreach (GpuDevice candidate in gpus) {
            if (candidate.VendorId == vendorId && 
                candidate.DeviceId == deviceId && 
                !assigned.ContainsKey(candidate.AdapterLuid)) {

                assigned[candidate.AdapterLuid] = 1;
                return candidate;
            }
        }

        return null;
    }

    private static string TargetName(int target) => target switch {
        NvApi.TargetGpu         => "GPU",
        NvApi.TargetMemory      => "Memory",
        NvApi.TargetPowerSupply => "Power Supply",
        NvApi.TargetBoard       => "Board",
                              _ => "Sensor"
    };

    public void Dispose()
    {
        if (!initialised) {
            return;
        }

        try {
            NvApi.Unload();
        }
        catch (DllNotFoundException) {
            // Driver dropped.
        }

        initialised = false;
    }
}
#endif
