#if __APPLE__
using System.Diagnostics;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Power.Providers.MacOS;

internal sealed class IOReportEnergySampler : IDisposable
{
    private const string EnergyModelGroup = "Energy Model";

    private readonly Lock @lock = new();

    private nint channels;
    private nint subscription;
    private nint previousSample;
    private long previousTimestamp;
    private bool subscribeFailed;

    public readonly record struct Delta(string Channel, string? Unit, long Value);

    public void Dispose()
    {
        lock (@lock) {
            if (previousSample != nint.Zero) {
                CoreFoundation.CFRelease(previousSample);
            }

            if (channels != nint.Zero) {
                CoreFoundation.CFRelease(channels);
            }

            previousSample  = nint.Zero;
            channels        = nint.Zero;
            subscription    = nint.Zero;
            subscribeFailed = false;
        }
    }

    public bool EnsureSubscribed()
    {
        lock (@lock) {
            if (subscription != nint.Zero) {
                return true;
            }

            if (subscribeFailed) {
                return false;
            }

            using CFScope group     = new(CoreFoundation.CFStringCreate(EnergyModelGroup));
            using CFScope available = new(IOReport.IOReportCopyChannelsInGroup(group, nint.Zero, 0, 0, 0));

            if (available.IsNull) {
                subscribeFailed = true;
                return false;
            }

            nint mutableChannels = CoreFoundation.CFDictionaryCreateMutableCopy(
                nint.Zero, CoreFoundation.CFDictionaryGetCount(available), available);

            if (mutableChannels == nint.Zero) {
                subscribeFailed = true;
                return false;
            }

            nint created = IOReport.IOReportCreateSubscription(nint.Zero, mutableChannels, out _, 0, nint.Zero);

            if (created == nint.Zero) {
                CoreFoundation.CFRelease(mutableChannels);
                subscribeFailed = true;
                return false;
            }

            channels     = mutableChannels;
            subscription = created;
            return true;
        }
    }

    private static List<Delta> ReadDeltas(nint delta)
    {
        List<Delta> deltas = new();

        if (!CoreFoundation.ToDictionary(delta).TryGetValue("IOReportChannels", out nint items)) {
            return deltas;
        }

        long count = CoreFoundation.CFArrayGetCount(items);

        for (long i = 0; i < count; i++) {
            nint item = CoreFoundation.CFArrayGetValueAtIndex(items, i);

            if (CoreFoundation.GetString(IOReport.IOReportChannelGetChannelName(item)) is not { } channel) {
                continue;
            }

            deltas.Add(new Delta(
                channel,
                CoreFoundation.GetString(IOReport.IOReportChannelGetUnitLabel(item)),
                IOReport.IOReportSimpleGetIntegerValue(item, 0)));
        }

        return deltas;
    }

    public List<Delta>? Sample(out double seconds)
    {
        seconds = 0;

        lock (@lock) {
            if (subscription == nint.Zero) {
                return null;
            }

            nint sample    = IOReport.IOReportCreateSamples(subscription, channels, nint.Zero);
            long timestamp = Stopwatch.GetTimestamp();

            if (sample == nint.Zero) {
                return null;
            }

            nint previous = previousSample;

            previousSample    = sample;
            seconds           = Stopwatch.GetElapsedTime(previousTimestamp, timestamp).TotalSeconds;
            previousTimestamp = timestamp;

            if (previous == nint.Zero) {
                return null;
            }

            try {
                using CFScope delta = new(IOReport.IOReportCreateSamplesDelta(previous, sample, nint.Zero));

                return delta.IsNull ? null : ReadDeltas(delta);
            }
            finally {
                CoreFoundation.CFRelease(previous);
            }
        }
    }
}
#endif
