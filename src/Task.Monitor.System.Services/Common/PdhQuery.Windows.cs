#if __WIN32__
using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Common;

internal sealed unsafe class PdhQuery : IDisposable
{
    private const int MaxReadAttempts = 3;

    private readonly string owner;

    private nint buffer;
    private uint bufferSize;
    private nint query;

    private delegate uint ReadArray(
        nint counter, 
        uint* size, 
        uint* itemCount, 
        nint items);

    private PdhQuery(nint query, IReadOnlyList<nint> counters, string owner)
    {
        this.query    = query;
        this.owner    = owner;
        Counters      = counters;
    }

    public nint Buffer => buffer;

    public bool Collect()
    {
        if (TryCollect(out uint result)) {
            return true;
        }

        PInvokeErrorHelpers.TraceOnceOnPInvokeError(
            $"{nameof(Pdh.PdhCollectQueryData)} {owner}",
            $"Failed {owner}",
            result);

        return false;
    }

    public IReadOnlyList<nint> Counters { get; }

    public void Dispose()
    {
        if (buffer != nint.Zero) {
            Marshal.FreeHGlobal(buffer);
            buffer     = nint.Zero;
            bufferSize = 0;
        }

        if (query != nint.Zero) {
            Pdh.PdhCloseQuery(query);
            query = nint.Zero;
        }
    }

    public static PdhQuery? Open(string owner, params string[] counterPaths)
    {
        PdhQuery? opened = Open(owner, out string failure, counterPaths);

        if (opened is null) {
            TraceEx.WriteLineOnce($"{nameof(PdhQuery)} {owner} {failure}", $"Failed {owner}: {failure}");
        }

        return opened;
    }

    public static PdhQuery? Open(string owner, out string failure, params string[] counterPaths)
    {
        nint query = nint.Zero;
        uint result = Pdh.PdhOpenQuery(null, nint.Zero, &query);

        if (result != Pdh.ERROR_SUCCESS) {
            failure = $"{nameof(Pdh.PdhOpenQuery)} {ErrorCodes.Describe(result)}";
            return null;
        }

        nint[] counters = new nint[counterPaths.Length];

        for (int i = 0; i < counterPaths.Length; i++) {
            nint counter = nint.Zero;
            result = Pdh.PdhAddEnglishCounter(query, counterPaths[i], nint.Zero, &counter);

            if (result != Pdh.ERROR_SUCCESS) {
                failure = $"{nameof(Pdh.PdhAddEnglishCounter)} {counterPaths[i]} {ErrorCodes.Describe(result)}";

                Pdh.PdhCloseQuery(query);
                return null;
            }

            counters[i] = counter;
        }

        failure = string.Empty;
        return new PdhQuery(query, counters, owner);
    }

    public static PdhQuery? OpenAtStartup(string serviceName, string label, params string[] counterPaths)
    {
        PdhQuery? opened = Open(serviceName, out string failure, counterPaths);

        Trace.WriteLine(opened is null
            ? $"{serviceName}: PDH {label} FAILED, {failure}"
            : $"{serviceName}: PDH {label} OK");

        return opened;
    }

    public bool TryCollect(out uint status)
    {
        status = Pdh.PdhCollectQueryData(query);
        return status == Pdh.ERROR_SUCCESS;
    }

    private bool TryRead(nint counter, string api, ReadArray read, out uint itemCount)
    {
        itemCount = 0;

        for (int attempt = 0; attempt < MaxReadAttempts; attempt++) {
            uint size  = bufferSize;
            uint count = 0;

            uint result = read(counter, &size, &count, buffer);

            if (result == Pdh.ERROR_SUCCESS) {
                itemCount = count;
                return true;
            }

            if (result != unchecked((uint)Pdh.PDH_MORE_DATA)) {
                PInvokeErrorHelpers.TraceOnceOnPInvokeError($"{api} {owner}", $"Failed {owner}", result);
                return false;
            }

            if (size <= bufferSize) {
                TraceEx.WriteLineOnce($"{api} {owner}", $"{api} failed to calculate the buffer size for {owner}");
                return false;
            }

            if (buffer != nint.Zero) {
                Marshal.FreeHGlobal(buffer);
            }

            buffer     = Marshal.AllocHGlobal((int)size);
            bufferSize = size;
        }

        return false;
    }

    public bool TryReadFormattedDouble(nint counter, out double value)
    {
        value = 0.0;

        if (!TryReadFormattedDoubles(counter, out uint itemCount) || itemCount == 0) {
            return false;
        }

        Pdh.PDH_FMT_COUNTERVALUE_ITEM_W item = *(Pdh.PDH_FMT_COUNTERVALUE_ITEM_W*)buffer;

        if (item.CStatus != Pdh.PDH_CSTATUS_VALID_DATA && item.CStatus != Pdh.PDH_CSTATUS_NEW_DATA) {
            return false;
        }

        value = item.doubleValue;
        return true;
    }

    public bool TryReadFormattedDoubles(nint counter, out uint itemCount) =>
        TryRead(
            counter,
            nameof(Pdh.PdhGetFormattedCounterArrayW),
            static (counter, size, count, items) =>
                unchecked((uint)Pdh.PdhGetFormattedCounterArrayW(counter, Pdh.PDH_FMT_DOUBLE, size, count, items)),
            out itemCount);

    public bool TryReadRaw(nint counter, out uint itemCount) =>
        TryRead(
            counter,
            nameof(Pdh.PdhGetRawCounterArray),
            static (counter, size, count, items) => Pdh.PdhGetRawCounterArray(counter, size, count, items),
            out itemCount);
}
#endif
