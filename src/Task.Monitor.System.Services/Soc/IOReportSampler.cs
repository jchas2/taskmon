#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Soc;

internal sealed class IOReportSampler(params (string Group, string SubGroup)[] groups) : IDisposable
{
    private readonly Lock @lock = new();

    private nint channels;
    private nint subscription;
    private nint previousSample;
    private bool subscribeFailed;

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

            nint merged = nint.Zero;

            foreach ((string group, string subGroup) in groups) {
                using CFScope groupName    = new(CoreFoundation.CFStringCreate(group));
                using CFScope subGroupName = new(CoreFoundation.CFStringCreate(subGroup));
                using CFScope available    = new(IOReport.IOReportCopyChannelsInGroup(groupName, subGroupName, 0, 0, 0));

                if (available.IsNull) {
                    continue;
                }

                if (merged == nint.Zero) {
                    merged = CoreFoundation.CFDictionaryCreateMutableCopy(nint.Zero, 0, available);
                }
                else {
                    IOReport.IOReportMergeChannels(merged, available, nint.Zero);
                }
            }

            if (merged == nint.Zero) {
                subscribeFailed = true;
                return false;
            }

            nint created = IOReport.IOReportCreateSubscription(nint.Zero, merged, out _, 0, nint.Zero);

            if (created == nint.Zero) {
                CoreFoundation.CFRelease(merged);
                subscribeFailed = true;
                return false;
            }

            channels     = merged;
            subscription = created;
            return true;
        }
    }

    private static List<IOReportChannelSample> ReadChannels(nint delta)
    {
        List<IOReportChannelSample> samples = new();

        if (!CoreFoundation.ToDictionary(delta).TryGetValue("IOReportChannels", out nint items)) {
            return samples;
        }

        long count = CoreFoundation.CFArrayGetCount(items);

        for (long i = 0; i < count; i++) {
            nint item = CoreFoundation.CFArrayGetValueAtIndex(items, i);
            int format = IOReport.IOReportChannelGetFormat(item);

            List<(string Name, long Residency)> states = new();
            long value = 0;

            if (format == IOReport.FormatState) {
                int stateCount = IOReport.IOReportStateGetCount(item);

                for (int state = 0; state < stateCount; state++) {
                    states.Add((
                        CoreFoundation.GetString(IOReport.IOReportStateGetNameForIndex(item, state))?.Trim() ?? string.Empty,
                        IOReport.IOReportStateGetResidency(item, state)));
                }
            }
            else if (format == IOReport.FormatSimple) {
                value = IOReport.IOReportSimpleGetIntegerValue(item, 0);
            }

            samples.Add(new IOReportChannelSample(
                CoreFoundation.GetString(IOReport.IOReportChannelGetGroup(item)) ?? string.Empty,
                CoreFoundation.GetString(IOReport.IOReportChannelGetSubGroup(item)) ?? string.Empty,
                CoreFoundation.GetString(IOReport.IOReportChannelGetChannelName(item)) ?? string.Empty,
                value,
                states));
        }

        return samples;
    }

    // The change since the previous call; the first call only takes the baseline.
    public List<IOReportChannelSample>? Sample()
    {
        lock (@lock) {
            if (subscription == nint.Zero) {
                return null;
            }

            nint sample = IOReport.IOReportCreateSamples(subscription, channels, nint.Zero);

            if (sample == nint.Zero) {
                return null;
            }

            nint previous = previousSample;
            previousSample = sample;

            if (previous == nint.Zero) {
                return null;
            }

            using CFScope previousScope = new(previous);
            using CFScope delta         = new(IOReport.IOReportCreateSamplesDelta(previous, sample, nint.Zero));

            return delta.IsNull ? null : ReadChannels(delta);
        }
    }
}
#endif
