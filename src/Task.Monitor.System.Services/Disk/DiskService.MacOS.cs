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
        using CFScope cfKey = new(CoreFoundation.CFStringCreate(key));
        
        using CFScope value = new(IOKit.IORegistryEntrySearchCFProperty(
            entry, 
            IOServicePlane, 
            cfKey, 
            nint.Zero, 
            options));

        return CoreFoundation.GetString(value);
    }

    private static bool SearchNumberProperty(
        uint entry, 
        string key, 
        uint options, 
        out long value)
    {
        value = 0;

        using CFScope cfKey = new(CoreFoundation.CFStringCreate(key));
        
        using CFScope number = new(IOKit.IORegistryEntrySearchCFProperty(
            entry, 
            IOServicePlane, 
            cfKey, 
            nint.Zero, 
            options));

        if (number.IsNull) {
            return false;
        }

        CoreFoundation.CFNumberGetValue(number, out value);
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
