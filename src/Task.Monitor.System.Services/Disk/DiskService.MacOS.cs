#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Disk;

// Shared macOS helpers for the DiskService partials. Metrics live in DiskService.DiskMetrics.MacOS.cs
// and specs/volumes in DiskService.DiskSpecs.MacOS.cs.
public partial class DiskService
{
    private const string BlockStorageDriverClass = "IOBlockStorageDriver";
    private const string IOServicePlane = "IOService";

    private const uint IterateRecursively = 0x00000001;
    private const uint IterateParents     = 0x00000002;

    // Searches the entry (and, per options, its children/parents) for a CFString-valued property.
    private static string? SearchStringProperty(uint entry, string key, uint options)
    {
        IntPtr cfKey = CoreFoundation.CFStringCreate(key);
        IntPtr value = IOKit.IORegistryEntrySearchCFProperty(entry, IOServicePlane, cfKey, IntPtr.Zero, options);
        CoreFoundation.CFRelease(cfKey);

        if (value == IntPtr.Zero) {
            return null;
        }

        string? result = CoreFoundation.GetString(value);
        CoreFoundation.CFRelease(value);
        return result;
    }

    // Searches for a CFNumber-valued property.
    private static bool SearchNumberProperty(uint entry, string key, uint options, out long value)
    {
        value = 0;

        IntPtr cfKey = CoreFoundation.CFStringCreate(key);
        IntPtr number = IOKit.IORegistryEntrySearchCFProperty(entry, IOServicePlane, cfKey, IntPtr.Zero, options);
        CoreFoundation.CFRelease(cfKey);

        if (number == IntPtr.Zero) {
            return false;
        }

        CoreFoundation.CFNumberGetValue(number, out value);
        CoreFoundation.CFRelease(number);
        return true;
    }

    // "disk0", "disk12" -> 0, 12; anything else -> -1.
    private static int ParseDiskIndex(string? bsdName) =>
        !string.IsNullOrEmpty(bsdName) &&
        bsdName.StartsWith("disk", StringComparison.Ordinal) &&
        int.TryParse(bsdName.AsSpan(4), out int index)
            ? index
            : -1;
}
#endif
