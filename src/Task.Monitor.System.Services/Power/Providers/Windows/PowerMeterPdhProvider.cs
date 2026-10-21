#if __WIN32__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Power.Providers.Windows;

// The ACPI / EMI power meter, exposed as the "Power Meter" performance counter set. Present on
// many laptops and a few desktops; reports whole-system or a platform rail, in milliwatts.
internal sealed unsafe class PowerMeterPdhProvider : IPowerProvider
{
    private const string CounterPath = @"\Power Meter(*)\Power";

    private PdhQuery? query;
    private nint      counter;

    public void Dispose()
    {
        query?.Dispose();
        query   = null;
        counter = nint.Zero;
    }

    public string Name => "ACPI Power Meter";

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

    public IEnumerable<PowerReading> Read()
    {
        if (query is null || ReadWatts() is not { } watts) {
            return [];
        }

        return [
            new PowerReading {
                Component = PowerComponent.System,
                Rail     = "System",
                Watts    = watts,
                Source   = PowerSource.PowerMeter
            }
        ];
    }

    private double? ReadWatts()
    {
        if (query is null || !query.Collect() || !query.TryReadFormattedDoubles(counter, out uint itemCount)) {
            return null;
        }

        Pdh.PDH_FMT_COUNTERVALUE_ITEM_W* items = (Pdh.PDH_FMT_COUNTERVALUE_ITEM_W*)query.Buffer;
        
        double summed = 0;
        bool   any    = false;

        for (uint i = 0; i < itemCount; i++) {
            Pdh.PDH_FMT_COUNTERVALUE_ITEM_W item = items[i];

            if (item.CStatus != Pdh.PDH_CSTATUS_VALID_DATA) {
                continue;
            }

            string? name = Marshal.PtrToStringUni(item.szName);

            if (Pdh.IsTotalInstance(name)) {
                return Normalise(item.doubleValue);
            }

            summed += item.doubleValue;
            any = true;
        }

        return any ? Normalise(summed) : null;
    }

    public bool TryInitialise()
    {
        query = PdhQuery.Open(nameof(PowerMeterPdhProvider), CounterPath);

        if (query is null) {
            return false;
        }

        counter = query.Counters[0];
        query.Collect();

        // Keep the provider only if the machine actually has a power meter instance.
        return ReadWatts() is not null;
    }
}
#endif
