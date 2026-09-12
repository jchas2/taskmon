#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Thermal;

// NVIDIA GPUs via NVAPI's GetThermalSettings. No elevation. Handles are matched back to a DXGI
// adapter (and therefore its AdapterLuid) by PCI vendor + device id.
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

            GpuDevice? gpu = MatchGpu(gpus, vendorId, deviceId, assignedByLuid);
            string componentId = gpu?.AdapterLuid.ToString() ?? $"nvapi{i}";

            foreach (NvApi.ThermalReading reading in NvApi.GetThermalSettings(handles[i])) {
                yield return new ThermalSensor {
                    Component = ThermalComponent.Gpu,
                    ComponentId = componentId,
                    SensorName = TargetName(reading.Target),
                    Celsius = reading.Celsius,
                    Source = ThermalSource.NvApi
                };
            }
        }
    }

    private static GpuDevice? MatchGpu(
        IReadOnlyList<GpuDevice> gpus, uint vendorId, uint deviceId, Dictionary<long, int> assigned)
    {
        // Match on PCI ids; if several identical cards match, hand out each GpuDevice once.
        foreach (GpuDevice candidate in gpus) {
            if (candidate.VendorId == vendorId
                && candidate.DeviceId == deviceId
                && !assigned.ContainsKey(candidate.AdapterLuid)) {

                assigned[candidate.AdapterLuid] = 1;
                return candidate;
            }
        }

        return null;
    }

    private static string TargetName(int target) => target switch {
        NvApi.TargetGpu => "GPU",
        NvApi.TargetMemory => "Memory",
        NvApi.TargetPowerSupply => "Power Supply",
        NvApi.TargetBoard => "Board",
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
            // Driver went away - nothing to unload.
        }

        initialised = false;
    }
}

// AMD GPUs via ADL's Overdrive temperature calls. No elevation. Adapters are matched to a DXGI
// adapter by PCI device id parsed out of the ADL PNP string.
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
            if (!Adl.TryGetTemperatureCelsius(context, adapter.AdapterIndex, out double celsius)) {
                continue;
            }

            GpuDevice? gpu = gpus.FirstOrDefault(candidate =>
                candidate.DeviceId == adapter.DeviceId && assigned.Add(candidate.AdapterLuid));

            yield return new ThermalSensor {
                Component = ThermalComponent.Gpu,
                ComponentId = gpu?.AdapterLuid.ToString() ?? $"adl{adapter.AdapterIndex}",
                SensorName = "GPU",
                Celsius = celsius,
                Source = ThermalSource.Adl
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
