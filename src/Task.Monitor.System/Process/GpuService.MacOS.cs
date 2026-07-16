using System.Diagnostics;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Process;

public static partial class GpuService
{
#if __APPLE__
    private static Dictionary<int, long> GetProcessStatsInternal()
    {
        long typeIdCFArray = CoreFoundation.CFArrayGetTypeID();
        long typeIdCFDictionary = CoreFoundation.CFDictionaryGetTypeID();
        
        Dictionary<int, long> gpuInfo = new();
        IntPtr matching = IOKit.IOServiceMatching("IOAccelerator");
        uint accelerator = IOKit.IOServiceGetMatchingService(0, matching);

        if (accelerator == 0) {
            Trace.WriteLine($"Failed to get IOAccelerator via IOServiceGetMatchingService in {nameof(GpuService)}.");
            return gpuInfo;
        }

        IntPtr iterator = IntPtr.Zero;

        int result = IOKit.IORegistryEntryGetChildIterator(
            accelerator, 
            "IOService", 
            ref iterator);

        if (result != 0 || iterator == IntPtr.Zero) {
            Trace.WriteLine($"Failed to get IOIterator via IORegistryEntryGetChildIterator in {nameof(GpuService)}.");
            return gpuInfo;
        }

        uint child;

        while ((child = IOKit.IOIteratorNext(iterator)) != 0) {
            IntPtr properties = IntPtr.Zero;

            result = IOKit.IORegistryEntryCreateCFProperties(
                child,
                out properties,
                IntPtr.Zero,
                0);

            if (result != 0 && properties == IntPtr.Zero) {
                continue;
            }

            Dictionary<string, nint> props = CoreFoundation.ToDictionary(properties);

            if (!props.ContainsKey("IOUserClientCreator") || !props.ContainsKey("AppUsage")) {
                CoreFoundation.CFRelease(properties);
                continue;
            }

            // IOUserClientCreator returns "pid nnnn, processname"
            string? creator = CoreFoundation.GetString(props["IOUserClientCreator"]);
            
            if (creator == null || !creator.StartsWith("pid ") || !creator.Contains(',')) {
                CoreFoundation.CFRelease(properties);
                continue;
            }

            int pid = -1;
            
            if (!int.TryParse(creator.AsSpan(4, creator.IndexOf(',') - 4), out pid)) {
                CoreFoundation.CFRelease(properties);
                continue;
            }
            
            long totalGpuTime = 0;
            IntPtr appUsage = props["AppUsage"];

            // AppUsage should be CFArray.
            if (CoreFoundation.CFGetTypeID(appUsage) != typeIdCFArray) { 
                CoreFoundation.CFRelease(properties);
                continue;
            }

            long count = CoreFoundation.CFArrayGetCount(appUsage);
            
            for (long i = 0; i < count; i++) {
                IntPtr element = CoreFoundation.CFArrayGetValueAtIndex(appUsage, i);
                
                // Element should be a CFDictionary.
                if (CoreFoundation.CFGetTypeID(element) == typeIdCFDictionary) { 
                    Dictionary<string, nint> elemDict = CoreFoundation.ToDictionary(element);
                    
                    if (elemDict.TryGetValue("accumulatedGPUTime", out var value)) {
                        CoreFoundation.CFNumberGetValue(value, out long gpuTime);
                        totalGpuTime += gpuTime;
                    }
                }
            }

            if (totalGpuTime > 0) {                                                                                                                                                                                          
                gpuInfo[pid] = gpuInfo.GetValueOrDefault(pid) + totalGpuTime;                                                                                                                          
            }          
            
            CoreFoundation.CFRelease(properties);
        }

        IOKit.IOObjectRelease(child);
        IOKit.IOObjectRelease(iterator);
        
        return gpuInfo;
    }
#endif
}