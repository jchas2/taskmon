using System.Diagnostics;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Gpu;

public partial class GpuService
{
#if __APPLE__
    private const double NanosecondsPerSecond = 1_000_000_000.0;

    private readonly Dictionary<int, long> previousGpuTime = new();
    
    private long previousGpuTimestamp;
    private bool gpuPrimed;

    private nint gpuReportSubscription = nint.Zero;
    private nint gpuReportChannels     = nint.Zero;
    private nint gpuReportPrevSample   = nint.Zero;
    
    private bool gpuReportInitFailed;

    private void OnStartGpuPidMetrics()
    {
        ResetGpuPidState();
        ReleaseGpuReportState();
    }

    private void OnStopGpuPidMetrics()
    {
        ResetGpuPidState();
        ReleaseGpuReportState();
    }

    private void ResetGpuPidState()
    {
        previousGpuTime.Clear();
        
        previousGpuTimestamp = 0;
        gpuPrimed            = false;
    }

    private void OnDoWorkGpuPidMetrics(GpuInfo gpuInfo)
    {
        UpdateDeviceGpuUsage(gpuInfo);

        Dictionary<int, long> current = GetProcessGpuTime();
        long now = Stopwatch.GetTimestamp();

        if (!gpuPrimed) {
            RebaseGpuPidState(current, now);
            gpuPrimed = true;
            return;
        }

        double elapsedSeconds = (now - previousGpuTimestamp) / (double)Stopwatch.Frequency;

        if (elapsedSeconds <= 0.0) {
            RebaseGpuPidState(current, now);
            return;
        }

        double elapsedNanos = elapsedSeconds * NanosecondsPerSecond;

        foreach ((int pid, long gpuTime) in current) {
            long previous = previousGpuTime.GetValueOrDefault(pid);
            long delta    = gpuTime > previous ? gpuTime - previous : 0;

            if (delta <= 0) {
                continue;
            }

            double fraction = Math.Clamp(delta / elapsedNanos, 0.0, 1.0);
            gpuInfo.Metrics.ProcessPercentTime[pid] = fraction;
        }

        RebaseGpuPidState(current, now);
    }

    private void RebaseGpuPidState(Dictionary<int, long> current, long now)
    {
        previousGpuTime.Clear();

        foreach ((int pid, long gpuTime) in current) {
            previousGpuTime[pid] = gpuTime;
        }

        previousGpuTimestamp = now;
    }

    private static Dictionary<int, long> GetProcessGpuTime()
    {
        long typeIdCFArray      = CoreFoundation.CFArrayGetTypeID();
        long typeIdCFDictionary = CoreFoundation.CFDictionaryGetTypeID();

        Dictionary<int, long> gpuInfo     = new();
        nint                  matching    = IOKit.IOServiceMatching("IOAccelerator");
        using IOObjectScope   accelerator = new(IOKit.IOServiceGetMatchingService(0, matching));

        if (accelerator.IsNull) {
            Trace.WriteLine($"Failed to get IOAccelerator via IOServiceGetMatchingService in {nameof(GpuService)}.");
            return gpuInfo;
        }

        nint iteratorRef = nint.Zero;

        int result = IOKit.IORegistryEntryGetChildIterator(
            accelerator,
            "IOService",
            ref iteratorRef);

        using IOObjectScope iterator = new(iteratorRef);

        if (result != 0 || iterator.IsNull) {
            Trace.WriteLine($"Failed to get IOIterator via IORegistryEntryGetChildIterator in {nameof(GpuService)}.");
            return gpuInfo;
        }

        uint childRef;

        while ((childRef = IOKit.IOIteratorNext(iterator)) != 0) {
            using IOObjectScope child = new(childRef);

            result = IOKit.IORegistryEntryCreateCFProperties(
                child,
                out nint propertiesRef,
                nint.Zero,
                0);

            using CFScope properties = new(propertiesRef);

            if (result != 0 || properties.IsNull) {
                continue;
            }

            Dictionary<string, nint> props = CoreFoundation.ToDictionary(properties);

            if (!props.ContainsKey("IOUserClientCreator") || !props.ContainsKey("AppUsage")) {
                continue;
            }

            // IOUserClientCreator returns "pid nnnn, processname".
            string? creator = CoreFoundation.GetString(props["IOUserClientCreator"]);

            if (creator == null || !creator.StartsWith("pid ") || !creator.Contains(',')) {
                continue;
            }

            if (!int.TryParse(creator.AsSpan(4, creator.IndexOf(',') - 4), out int pid)) {
                continue;
            }

            nint appUsage = props["AppUsage"];

            // AppUsage should be a CFArray of CFDictionary entries.
            if (CoreFoundation.CFGetTypeID(appUsage) != typeIdCFArray) {
                continue;
            }

            long totalGpuTime = 0;
            long count = CoreFoundation.CFArrayGetCount(appUsage);

            for (long i = 0; i < count; i++) {
                nint element = CoreFoundation.CFArrayGetValueAtIndex(appUsage, i);

                if (CoreFoundation.CFGetTypeID(element) == typeIdCFDictionary) {
                    Dictionary<string, nint> elemDict = CoreFoundation.ToDictionary(element);

                    if (elemDict.TryGetValue("accumulatedGPUTime", out nint value)) {
                        CoreFoundation.CFNumberGetValue(value, out long gpuTime);
                        totalGpuTime += gpuTime;
                    }
                }
            }

            if (totalGpuTime > 0) {
                gpuInfo[pid] = gpuInfo.GetValueOrDefault(pid) + totalGpuTime;
            }
        }

        return gpuInfo;
    }

    private bool EnsureGpuReportSubscription()
    {
        if (gpuReportInitFailed) {
            return false;
        }

        if (gpuReportSubscription != nint.Zero) {
            return true;
        }

        using CFScope group    = new(CoreFoundation.CFStringCreate("GPU Stats"));

        using CFScope channels = new(IOReport.IOReportCopyChannelsInGroup(
            group, 
            nint.Zero, 
            0, 
            0, 
            0));

        if (channels.IsNull) {
            gpuReportInitFailed = true;
            return false;
        }

        long count = CoreFoundation.CFDictionaryGetCount(channels);

        // Owned by gpuReportChannels once subscribed, so released manually rather than scoped.
        nint mutableChannels = CoreFoundation.CFDictionaryCreateMutableCopy(nint.Zero, count, channels);

        if (mutableChannels == nint.Zero) {
            gpuReportInitFailed = true;
            return false;
        }

        nint subscription = IOReport.IOReportCreateSubscription(
            nint.Zero,
            mutableChannels,
            out _,
            0,
            nint.Zero);

        if (subscription == nint.Zero) {
            CoreFoundation.CFRelease(mutableChannels);
            gpuReportInitFailed = true;
            return false;
        }

        gpuReportChannels     = mutableChannels;
        gpuReportSubscription = subscription;
        return true;
    }

    private void UpdateDeviceGpuUsage(GpuInfo gpuInfo)
    {
        if (!EnsureGpuReportSubscription()) {
            return;
        }

        nint sample = IOReport.IOReportCreateSamples(gpuReportSubscription, gpuReportChannels, nint.Zero);

        if (sample == nint.Zero) {
            return;
        }

        if (gpuReportPrevSample != nint.Zero) {
            using CFScope delta = new(IOReport.IOReportCreateSamplesDelta(gpuReportPrevSample, sample, nint.Zero));

            if (!delta.IsNull && TryGetGpuResidency(delta, out double active)) {
                gpuInfo.Metrics.GpuPercentTime = Math.Clamp(active, 0.0, 1.0);
            }

            CoreFoundation.CFRelease(gpuReportPrevSample);
        }

        gpuReportPrevSample = sample;
    }

    private static bool TryGetGpuResidency(nint deltaSample, out double activeFraction)
    {
        activeFraction = 0.0;
        
        Dictionary<string, nint> top = CoreFoundation.ToDictionary(deltaSample);

        if (!top.TryGetValue("IOReportChannels", out nint channelsArray)) {
            return false;
        }

        long channelCount = CoreFoundation.CFArrayGetCount(channelsArray);

        for (long i = 0; i < channelCount; i++) {
            nint item = CoreFoundation.CFArrayGetValueAtIndex(channelsArray, i);

            if (CoreFoundation.GetString(IOReport.IOReportChannelGetGroup(item)) != "GPU Stats") {
                continue;
            }

            if (CoreFoundation.GetString(IOReport.IOReportChannelGetSubGroup(item)) != "GPU Performance States") {
                continue;
            }

            if (CoreFoundation.GetString(IOReport.IOReportChannelGetChannelName(item)) != "GPUPH") {
                continue;
            }

            int stateCount  = IOReport.IOReportStateGetCount(item);
            long totalTime  = 0;
            long activeTime = 0;

            for (int s = 0; s < stateCount; s++) {
                long residency = IOReport.IOReportStateGetResidency(item, s);
                totalTime += residency;

                string? stateName = CoreFoundation.GetString(IOReport.IOReportStateGetNameForIndex(item, s));

                if (stateName != "OFF" && stateName != "IDLE" && stateName != "DOWN") {
                    activeTime += residency;
                }
            }

            if (totalTime > 0) {
                activeFraction = (double)activeTime / totalTime;
                return true;
            }
        }

        return false;
    }

    private void ReleaseGpuReportState()
    {
        if (gpuReportPrevSample != nint.Zero) {
            CoreFoundation.CFRelease(gpuReportPrevSample);
            gpuReportPrevSample = nint.Zero;
        }

        if (gpuReportChannels != nint.Zero) {
            CoreFoundation.CFRelease(gpuReportChannels);
            gpuReportChannels = nint.Zero;
        }

        // The IOReportSubscriptionRef is private API and is not safely CFReleasable here; it is left
        // to be reclaimed at process exit (one per service lifetime).
        gpuReportSubscription = nint.Zero;
        gpuReportInitFailed = false;
    }
#endif
}
