#if __WIN32__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Power.Providers.Windows;

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
#endif
