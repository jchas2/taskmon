#if __WIN32__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Power;

#pragma warning disable CA1416 // Validate platform compatibility

// NVIDIA GPUs via NVML - a measured, milliwatt reading. Handles are matched back to a DXGI adapter
// by PCI vendor + device id.
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

// AMD GPUs via ADL's Overdrive 6 total board power. GCN / Polaris / Vega; RDNA reports nothing here.
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
            if (!Adl.TryGetPowerWatts(context, adapter.AdapterIndex, out double watts)) {
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

// The ACPI / EMI power meter, exposed as the "Power Meter" performance counter set. Present on
// many laptops and a few desktops; reports whole-system or a platform rail, in milliwatts.
internal sealed unsafe class PowerMeterPdhProvider : IPowerProvider
{
    private const string CounterPath = @"\Power Meter(*)\Power";

    private nint query;
    private nint counter;
    private nint buffer;
    private uint bufferSize;

    public string Name => "ACPI Power Meter";

    public bool TryInitialise()
    {
        nint newQuery = nint.Zero;
        nint newCounter = nint.Zero;

        if (Pdh.PdhOpenQuery(null, nint.Zero, &newQuery) != Pdh.ERROR_SUCCESS) {
            return false;
        }

        if (Pdh.PdhAddEnglishCounter(newQuery, CounterPath, nint.Zero, &newCounter) != Pdh.ERROR_SUCCESS) {
            Pdh.PdhCloseQuery(newQuery);
            return false;
        }

        query = newQuery;
        counter = newCounter;

        Pdh.PdhCollectQueryData(query);

        // Keep the provider only if the machine actually has a power meter instance.
        return ReadWatts() is not null;
    }

    public IEnumerable<PowerReading> Read()
    {
        if (query == nint.Zero || ReadWatts() is not { } watts) {
            return [];
        }

        return [
            new PowerReading {
                Component = PowerComponent.System,
                Rail = "System",
                Watts = watts,
                Source = PowerSource.PowerMeter
            }
        ];
    }

    private double? ReadWatts()
    {
        if (Pdh.PdhCollectQueryData(query) != Pdh.ERROR_SUCCESS || !TryReadArray(out uint itemCount)) {
            return null;
        }

        Pdh.PDH_FMT_COUNTERVALUE_ITEM_W* items = (Pdh.PDH_FMT_COUNTERVALUE_ITEM_W*)buffer;
        double summed = 0;
        bool any = false;

        for (uint i = 0; i < itemCount; i++) {
            Pdh.PDH_FMT_COUNTERVALUE_ITEM_W item = items[i];

            if (item.CStatus != Pdh.PDH_CSTATUS_VALID_DATA) {
                continue;
            }

            string? name = Marshal.PtrToStringUni(item.szName);

            if (string.Equals(name, "_Total", StringComparison.OrdinalIgnoreCase)) {
                return Normalise(item.doubleValue);
            }

            summed += item.doubleValue;
            any = true;
        }

        return any ? Normalise(summed) : null;
    }

    // The counter is documented as milliwatts, but be forgiving: a value that only makes sense as
    // watts is taken as watts.
    private static double? Normalise(double raw)
    {
        double asMilliwatts = raw / 1000.0;

        if (asMilliwatts is > 0.5 and < 1000) {
            return asMilliwatts;
        }

        return raw is > 0.5 and < 1000 ? raw : null;
    }

    private bool TryReadArray(out uint itemCount)
    {
        itemCount = 0;

        for (int attempt = 0; attempt < 3; attempt++) {
            uint size = bufferSize;
            uint count = 0;

            int result = Pdh.PdhGetFormattedCounterArrayW(
                counter, Pdh.PDH_FMT_DOUBLE, &size, &count, buffer);

            if (result == (int)Pdh.ERROR_SUCCESS) {
                itemCount = count;
                return true;
            }

            if (result != Pdh.PDH_MORE_DATA || size <= bufferSize) {
                return false;
            }

            if (buffer != nint.Zero) {
                Marshal.FreeHGlobal(buffer);
            }

            buffer = Marshal.AllocHGlobal((int)size);
            bufferSize = size;
        }

        return false;
    }

    public void Dispose()
    {
        if (buffer != nint.Zero) {
            Marshal.FreeHGlobal(buffer);
            buffer = nint.Zero;
            bufferSize = 0;
        }

        if (query != nint.Zero) {
            Pdh.PdhCloseQuery(query);
            query = nint.Zero;
            counter = nint.Zero;
        }
    }
}

// Whole-system power while the machine is running on battery. Silent on AC / on a desktop.
internal sealed class BatteryPowerProvider : IPowerProvider
{
    public string Name => "Battery";

    public bool TryInitialise() => true;

    public IEnumerable<PowerReading> Read()
    {
        if (PowerBase.SystemDischargeWatts() is { } watts) {
            yield return new PowerReading {
                Component = PowerComponent.System,
                Rail = "System (battery)",
                Watts = watts,
                Source = PowerSource.Battery
            };
        }
    }

    public void Dispose() { }
}

// The nameplate peak power of an NVMe drive's top power state - not a live draw. Read once per
// drive from the Identify Controller structure and cached.
internal sealed class NvmeRatedPowerProvider(Func<IReadOnlyList<DiskDevice>> getDisks) : IPowerProvider
{
    private readonly Dictionary<int, double?> cache = new();

    public string Name => "NVMe rated power";

    public bool TryInitialise() => true;

    public IEnumerable<PowerReading> Read()
    {
        foreach (DiskDevice disk in getDisks()) {
            if (!string.Equals(disk.BusType, "NVMe", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            if (!cache.TryGetValue(disk.Index, out double? watts)) {
                watts = ReadPeakWatts(disk.Index);
                cache[disk.Index] = watts;
            }

            if (watts is { } value) {
                yield return new PowerReading {
                    Component = PowerComponent.Disk,
                    ComponentId = disk.Index.ToString(),
                    Rail = "Drive",
                    Watts = value,
                    IsRated = true,
                    Source = PowerSource.NvmeRated
                };
            }
        }
    }

    private static double? ReadPeakWatts(int physicalDriveIndex)
    {
        byte[] identify = new byte[WinIoCtl.NVMeIdentifyControllerSize];

        return Nvme.TryQueryIdentify(physicalDriveIndex, WinIoCtl.NVMeIdentifyCnsController, identify)
            ? NvmePowerState.PeakWatts(identify)
            : null;
    }

    public void Dispose() { }
}

#pragma warning restore CA1416
#endif
