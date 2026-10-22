#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Power.Providers.MacOS;

internal sealed class SmartBatteryPowerProvider : IPowerProvider
{
    private const string ServiceName    = "AppleSmartBattery";
    private const string IOServicePlane = "IOService";

    private uint battery;

    public void Dispose()
    {
        if (battery != 0) {
            IOKit.IOObjectRelease(battery);
            battery = 0;
        }
    }

    public static bool IsPresent()
    {
        using IOObjectScope service = new(IOKit.IOServiceGetMatchingService(0, IOKit.IOServiceMatching(ServiceName)));

        return !service.IsNull;
    }

    public string Name => "Smart Battery";

    public IEnumerable<PowerReading> Read()
    {
        if (battery == 0) {
            yield break;
        }

        bool onAdapter = ReadProperty("ExternalConnected") is true;

        double? watts = SystemWatts(
            ReadProperty("PowerTelemetryData"),
            ReadProperty("Voltage"),
            ReadProperty("InstantAmperage"),
            onAdapter);

        if (watts is { } value) {
            yield return new PowerReading {
                Component = PowerComponent.System,
                Rail      = onAdapter ? PowerRails.SystemAdapter : PowerRails.SystemBattery,
                Watts     = value,
                Source    = PowerSource.SmartBattery
            };
        }
    }

    private object? ReadProperty(string key)
    {
        using CFScope name  = new(CoreFoundation.CFStringCreate(key));
        using CFScope value = new(IOKit.IORegistryEntrySearchCFProperty(battery, IOServicePlane, name, nint.Zero, 0));

        return value.IsNull ? null : PropertyList.ToManaged(value);
    }

    internal static double? SystemWatts(object? telemetry, object? voltage, object? instantAmperage, bool onAdapter)
    {
        if (telemetry is IReadOnlyDictionary<string, object?> values &&
            values.GetValueOrDefault("SystemLoad") is long milliwatts &&
            milliwatts > 0) {

            return milliwatts / 1000.0;
        }

        if (!onAdapter && voltage is long millivolts && instantAmperage is long milliamps && milliamps < 0) {
            return millivolts * -milliamps / 1_000_000.0;
        }

        return null;
    }

    public bool TryInitialise()
    {
        battery = IOKit.IOServiceGetMatchingService(0, IOKit.IOServiceMatching(ServiceName));

        return battery != 0;
    }
}
#endif
