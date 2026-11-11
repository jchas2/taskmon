#if __WIN32__
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Power.Providers.Windows;

// The ACPI / EMI power meter, present on laptops.
internal sealed unsafe class PowerMeterPdhProvider : IPowerProvider
{
    private const string CounterPath = @"\Power Meter(*)\Power";

    private const uint PdhNoData = 0x800007D5;

    private PdhQuery? query;
    private nint      counter;

    public string Diagnostic { get; private set; } = string.Empty;

    public void Dispose()
    {
        query?.Dispose();
        query   = null;
        counter = nint.Zero;
    }

    public string Name => "ACPI Power Meter";

    private static double? Normalise(double raw)
    {
        // The counter is documented as milliwatts.
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

    private double? ReadCollectedWatts()
    {
        if (query is null || !query.TryReadFormattedDoubles(counter, out uint itemCount)) {
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

    private double? ReadWatts() =>
        query is not null && query.Collect()
            ? ReadCollectedWatts()
            : null;

    public bool TryInitialise()
    {
        query = PdhQuery.Open(nameof(PowerMeterPdhProvider), out string failure, CounterPath);

        if (query is null) {
            Diagnostic = $"FAILED, {failure}";
            return false;
        }

        counter = query.Counters[0];

        query.TryCollect(out _);

        if (!query.TryCollect(out uint status)) {
            Diagnostic = status == PdhNoData
                ? "NOT FOUND, no power meter on this machine"
                : $"FAILED, {nameof(Pdh.PdhCollectQueryData)} {ErrorCodes.Describe(status)}";

            return false;
        }

        if (ReadCollectedWatts() is null) {
            Diagnostic = "NOT FOUND, the power meter has no reading";
            return false;
        }

        Diagnostic = "OK";
        return true;
    }
}
#endif
