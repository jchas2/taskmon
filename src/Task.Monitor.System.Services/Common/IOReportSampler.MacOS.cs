#if __APPLE__
using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Native;

namespace Task.Monitor.System.Services.Common;

internal sealed record IOReportChannelSample(
    string                                       Group,
    string                                       SubGroup,
    string                                       Name,
    long                                         Value,
    IReadOnlyList<(string Name, long Residency)> States,
    string?                                      Unit = null);

internal sealed class IOReportSampler(string owner, params (string Group, string? SubGroup)[] groups) : IDisposable
{
    private readonly Lock @lock = new();

    private nint channels;
    private nint subscription;
    private nint previousSample;
    private long previousTimestamp;
    private bool subscribeFailed;

    public string Diagnostic { get; private set; } = string.Empty;

    public void Dispose()
    {
        lock (@lock) {
            if (previousSample != nint.Zero) {
                CoreFoundation.CFRelease(previousSample);
            }

            if (channels != nint.Zero) {
                CoreFoundation.CFRelease(channels);
            }

            // The IOReportSubscriptionRef is private API and is not safely CFReleasable, so it is left
            // to be reclaimed at process exit (one per service lifetime).
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

            foreach ((string group, string? subGroup) in groups) {
                using CFScope groupName    = new(CFConvert.CreateString(group));
                using CFScope subGroupName = new(subGroup is null ? nint.Zero : CFConvert.CreateString(subGroup));
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
                Diagnostic      = "NOT FOUND, none of the IOReport channel groups";
                subscribeFailed = true;
                return false;
            }

            nint created = IOReport.IOReportCreateSubscription(nint.Zero, merged, out _, 0, nint.Zero);

            if (created == nint.Zero) {
                CoreFoundation.CFRelease(merged);
                Diagnostic      = "FAILED, IOReportCreateSubscription returned no subscription";
                subscribeFailed = true;
                return false;
            }

            channels     = merged;
            subscription = created;
            Diagnostic   = "OK";
            return true;
        }
    }

    private static List<IOReportChannelSample> ReadChannels(nint delta)
    {
        List<IOReportChannelSample> samples = new();

        if (!CFConvert.ToDictionary(delta).TryGetValue("IOReportChannels", out nint items)) {
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
                        CFConvert.GetString(IOReport.IOReportStateGetNameForIndex(item, state))?.Trim() ?? string.Empty,
                        IOReport.IOReportStateGetResidency(item, state)));
                }
            }
            else {
                value = IOReport.IOReportSimpleGetIntegerValue(item, 0);
            }

            samples.Add(new IOReportChannelSample(
                CFConvert.GetString(IOReport.IOReportChannelGetGroup(item)) ?? string.Empty,
                CFConvert.GetString(IOReport.IOReportChannelGetSubGroup(item)) ?? string.Empty,
                CFConvert.GetString(IOReport.IOReportChannelGetChannelName(item)) ?? string.Empty,
                value,
                states,
                CFConvert.GetString(IOReport.IOReportChannelGetUnitLabel(item))));
        }

        return samples;
    }

    public List<IOReportChannelSample>? Sample(out double seconds)
    {
        seconds = 0;

        lock (@lock) {
            if (subscription == nint.Zero) {
                return null;
            }

            nint sample    = IOReport.IOReportCreateSamples(subscription, channels, nint.Zero);
            long timestamp = Stopwatch.GetTimestamp();

            if (sample == nint.Zero) {
                TraceEx.WriteLineOnce(
                    $"{nameof(IOReportSampler)} {owner}",
                    $"{owner}: IOReportCreateSamples returned no sample");

                return null;
            }

            nint previous = previousSample;

            previousSample    = sample;
            seconds           = Stopwatch.GetElapsedTime(previousTimestamp, timestamp).TotalSeconds;
            previousTimestamp = timestamp;

            if (previous == nint.Zero) {
                return null;
            }

            using CFScope previousScope = new(previous);
            using CFScope delta         = new(IOReport.IOReportCreateSamplesDelta(previous, sample, nint.Zero));

            return delta.IsNull ? null : ReadChannels(delta);
        }
    }

    public List<IOReportChannelSample>? Sample() => Sample(out _);
}
#endif
