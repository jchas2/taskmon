using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Native;
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.Soc;

namespace Task.Monitor.System.Services.Gpu;

public partial class GpuService
{
#if __APPLE__
    private const double NanosecondsPerSecond = 1_000_000_000.0;

    private readonly Dictionary<int, long> previousGpuTime = new();
    
    private long previousGpuTimestamp;
    private bool gpuPrimed;

    private readonly IOReportSampler gpuReport = new(nameof(GpuService), ("GPU Stats", "GPU Performance States"));

    private static Dictionary<int, long> GetProcessGpuTime(out string status)
    {
        long typeIdCFArray      = CoreFoundation.CFArrayGetTypeID();
        long typeIdCFDictionary = CoreFoundation.CFDictionaryGetTypeID();

        Dictionary<int, long> gpuInfo     = new();
        nint                  matching    = IOKit.IOServiceMatching("IOAccelerator");
        using IOObjectScope   accelerator = new(IOKit.IOServiceGetMatchingService(0, matching));

        if (accelerator.IsNull) {
            status = "NOT FOUND, no IOAccelerator service";
            TraceEx.WriteLineOnce($"{nameof(GpuService)} IOAccelerator", $"{nameof(GpuService)}: IOAccelerator {status}");
            return gpuInfo;
        }

        nint iteratorRef = nint.Zero;

        int result = IOKit.IORegistryEntryGetChildIterator(
            accelerator,
            "IOService",
            ref iteratorRef);

        using IOObjectScope iterator = new(iteratorRef);

        if (result != 0 || iterator.IsNull) {
            status = $"FAILED, IORegistryEntryGetChildIterator {ErrorCodes.DescribeKernReturn(result)}";
            TraceEx.WriteLineOnce($"{nameof(GpuService)} IOAccelerator children", $"{nameof(GpuService)}: IOAccelerator {status}");
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

            Dictionary<string, nint> props = CFConvert.ToDictionary(properties);

            if (!props.ContainsKey("IOUserClientCreator") || !props.ContainsKey("AppUsage")) {
                continue;
            }

            // IOUserClientCreator returns "pid nnnn, processname".
            string? creator = CFConvert.GetString(props["IOUserClientCreator"]);

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
                    Dictionary<string, nint> elemDict = CFConvert.ToDictionary(element);

                    if (elemDict.TryGetValue("accumulatedGPUTime", out nint value)) {
                        totalGpuTime += CFConvert.GetInt64(value);
                    }
                }
            }

            if (totalGpuTime > 0) {
                gpuInfo[pid] = gpuInfo.GetValueOrDefault(pid) + totalGpuTime;
            }
        }

        status = $"OK, {gpuInfo.Count} processes with GPU time";
        return gpuInfo;
    }

    private void OnDoWorkGpuPidMetrics(GpuInfo gpuInfo)
    {
        UpdateDeviceGpuUsage(gpuInfo);

        Dictionary<int, long> current = GetProcessGpuTime(out string timeStatus);

        if (IsFirstCycle) {
            Trace.WriteLine($"{ServiceName}: IOReport GPU Stats {gpuReport.Diagnostic}");
            Trace.WriteLine($"{ServiceName}: IOAccelerator per-process GPU time {timeStatus}");
        }
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

    private void OnStartGpuPidMetrics() => ResetGpuPidState();

    private void OnStopGpuPidMetrics()
    {
        ResetGpuPidState();
        gpuReport.Dispose();
    }

    private void RebaseGpuPidState(Dictionary<int, long> current, long now)
    {
        previousGpuTime.Clear();

        foreach ((int pid, long gpuTime) in current) {
            previousGpuTime[pid] = gpuTime;
        }

        previousGpuTimestamp = now;
    }

    private void ResetGpuPidState()
    {
        previousGpuTime.Clear();
        
        previousGpuTimestamp = 0;
        gpuPrimed            = false;
    }

    private void UpdateDeviceGpuUsage(GpuInfo gpuInfo)
    {
        if (!gpuReport.EnsureSubscribed() || gpuReport.Sample() is not { } channels) {
            return;
        }

        if (channels.FirstOrDefault(channel => channel.Name == "GPUPH") is { } performance &&
            SocMetricsCalculator.Share(performance.States, state => state is not ("OFF" or "IDLE" or "DOWN")) is { } active) {

            gpuInfo.Metrics.GpuPercentTime = Math.Clamp(active, 0.0, 1.0);
        }
    }
#endif
}
