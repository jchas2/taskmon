#if __WIN32__
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
        uint result = Pdh.PdhCollectQueryData(query);

        if (result == Pdh.ERROR_SUCCESS) {
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
        nint query = nint.Zero;
        uint result = Pdh.PdhOpenQuery(null, nint.Zero, &query);

        if (result != Pdh.ERROR_SUCCESS) {
            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                $"{nameof(Pdh.PdhOpenQuery)} {owner}",
                $"Failed {owner}",
                result);

            return null;
        }

        nint[] counters = new nint[counterPaths.Length];

        for (int i = 0; i < counterPaths.Length; i++) {
            nint counter = nint.Zero;
            result = Pdh.PdhAddEnglishCounter(query, counterPaths[i], nint.Zero, &counter);

            if (result != Pdh.ERROR_SUCCESS) {
                PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                    counterPaths[i],
                    $"Failed {nameof(Pdh.PdhAddEnglishCounter)} {owner}",
                    result);

                Pdh.PdhCloseQuery(query);
                return null;
            }

            counters[i] = counter;
        }

        return new PdhQuery(query, counters, owner);
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
