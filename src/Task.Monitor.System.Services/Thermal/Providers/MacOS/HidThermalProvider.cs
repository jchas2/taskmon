#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Thermal.Providers.MacOS;

// The internal SSD's NAND sensors, which the SMC does not name.
internal sealed class HidThermalProvider(Func<IReadOnlyList<DiskDevice>> getDisks) : IThermalProvider
{
    private const string InternalBusType = "Apple Fabric";
    private const string NandPrefix      = "NAND";
    private const string NandSensor      = "NAND";

    private readonly List<nint> nandServices = new();

    private nint client;
    private nint services;

    public void Dispose()
    {
        nandServices.Clear();

        if (services != nint.Zero) {
            CoreFoundation.CFRelease(services);
            services = nint.Zero;
        }

        if (client != nint.Zero) {
            CoreFoundation.CFRelease(client);
            client = nint.Zero;
        }
    }

    internal static string? InternalDiskId(IReadOnlyList<DiskDevice> disks) =>
        disks.FirstOrDefault(disk =>
            !disk.IsRemovable && string.Equals(disk.BusType, InternalBusType, StringComparison.OrdinalIgnoreCase))?
            .Index.ToString();

    internal static bool IsNandSensor(string? product) =>
        product?.StartsWith(NandPrefix, StringComparison.Ordinal) == true;

    private static bool IsTemperatureService(nint service) =>
        ReadProperty(service, "PrimaryUsagePage") is long page && page == IOHIDEventSystem.VendorUsagePage &&
        ReadProperty(service, "PrimaryUsage") is long usage && usage == IOHIDEventSystem.TemperatureUsage;

    public string Name => "HID Sensors";

    public IEnumerable<ThermalSensor> Read()
    {
        if (InternalDiskId(getDisks()) is not { } diskId) {
            yield break;
        }

        double? hottest = null;

        foreach (nint service in nandServices) {
            if (ReadCelsius(service) is { } celsius && celsius > (hottest ?? double.MinValue)) {
                hottest = celsius;
            }
        }

        if (hottest is { } value) {
            yield return new ThermalSensor {
                Component   = ThermalComponent.Disk,
                ComponentId = diskId,
                SensorName  = NandSensor,
                Celsius     = value,
                Source      = ThermalSource.HidSensor
            };
        }
    }

    private static double? ReadCelsius(nint service)
    {
        using CFScope hidEvent = new(IOHIDEventSystem.IOHIDServiceClientCopyEvent(
            service, IOHIDEventSystem.TemperatureEventType, 0, 0));

        if (hidEvent.IsNull) {
            return null;
        }

        double celsius = IOHIDEventSystem.IOHIDEventGetFloatValue(hidEvent, IOHIDEventSystem.TemperatureLevelField);

        return celsius is > 0 and < 150 ? celsius : null;
    }

    private static object? ReadProperty(nint service, string key)
    {
        using CFScope name  = new(CoreFoundation.CFStringCreate(key));
        using CFScope value = new(IOHIDEventSystem.IOHIDServiceClientCopyProperty(service, name));

        return value.IsNull ? null : PropertyList.ToManaged(value);
    }

    public bool TryInitialise()
    {
        try {
            client = IOHIDEventSystem.IOHIDEventSystemClientCreate(nint.Zero);
        }
        catch (EntryPointNotFoundException) {
            return false;
        }

        if (client == nint.Zero) {
            return false;
        }

        services = IOHIDEventSystem.IOHIDEventSystemClientCopyServices(client);

        if (services == nint.Zero) {
            return false;
        }

        long count = CoreFoundation.CFArrayGetCount(services);

        for (long i = 0; i < count; i++) {
            nint service = CoreFoundation.CFArrayGetValueAtIndex(services, i);

            if (IsTemperatureService(service) && IsNandSensor(ReadProperty(service, "Product") as string)) {
                nandServices.Add(service);
            }
        }

        return nandServices.Count > 0;
    }
}
#endif
