#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
    private const string BlockStorageDriverClass = "IOBlockStorageDriver";
    private const string IOServicePlane = "IOService";

    private const uint IterateRecursively = 0x00000001;
    private const uint IterateParents     = 0x00000002;

    private static string? SearchStringProperty(uint entry, string key, uint options)
    {
        nint cfKey = CoreFoundation.CFStringCreate(key);
        
        nint value = IOKit.IORegistryEntrySearchCFProperty(
            entry, 
            IOServicePlane, 
            cfKey, 
            nint.Zero, 
            options);
        
        CoreFoundation.CFRelease(cfKey);

        if (value == nint.Zero) {
            return null;
        }

        string? result = CoreFoundation.GetString(value);
        CoreFoundation.CFRelease(value);
        return result;
    }

    private static bool SearchNumberProperty(
        uint entry, 
        string key, 
        uint options, 
        out long value)
    {
        value = 0;

        nint cfKey = CoreFoundation.CFStringCreate(key);
        
        nint number = IOKit.IORegistryEntrySearchCFProperty(
            entry, 
            IOServicePlane, 
            cfKey, 
            nint.Zero, 
            options);
        
        CoreFoundation.CFRelease(cfKey);

        if (number == nint.Zero) {
            return false;
        }

        CoreFoundation.CFNumberGetValue(number, out value);
        CoreFoundation.CFRelease(number);
        return true;
    }

    // Parses "disk0", "disk12" -> 0, 12; otherwise  -1.
    private static int ParseDiskIndex(string? bsdName) =>
        !string.IsNullOrEmpty(bsdName) &&
        bsdName.StartsWith("disk", StringComparison.Ordinal) &&
        int.TryParse(bsdName.AsSpan(4), out int index)
            ? index
            : -1;
}
#endif
