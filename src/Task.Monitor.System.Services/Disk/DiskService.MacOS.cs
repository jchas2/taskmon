#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
    private const string BlockStorageDriverClass = "IOBlockStorageDriver";
    private const string IOServicePlane          = "IOService";
    private const string VirtualInterconnect     = "Virtual Interface";

    private const uint IterateRecursively = 0x00000001;
    private const uint IterateParents     = 0x00000002;

    internal static bool IsVirtualInterconnect(string? physicalInterconnect) =>
        string.Equals(physicalInterconnect, VirtualInterconnect, StringComparison.Ordinal);

    // Parses "disk0", "disk12" -> 0, 12; otherwise  -1.
    private static int ParseDiskIndex(string? bsdName) =>
        !string.IsNullOrEmpty(bsdName) &&
        bsdName.StartsWith("disk", StringComparison.Ordinal) &&
        int.TryParse(bsdName.AsSpan(4), out int index)
            ? index
            : -1;

    private static string? ReadPhysicalInterconnect(uint entry)
    {
        using CFScope cfKey = new(CoreFoundation.CFStringCreate("Protocol Characteristics"));

        using CFScope dictRef = new(IOKit.IORegistryEntrySearchCFProperty(
            entry,
            IOServicePlane,
            cfKey,
            nint.Zero,
            IterateRecursively | IterateParents));

        if (dictRef.IsNull ||
            !CoreFoundation.ToDictionary(dictRef).TryGetValue("Physical Interconnect", out nint value)) {

            return null;
        }

        string? text = CoreFoundation.GetString(value)?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static bool SearchNumberProperty(
        uint     entry, 
        string   key, 
        uint     options, 
        out long value)
    {
        value = 0;

        using CFScope cfKey  = new(CoreFoundation.CFStringCreate(key));
        
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
}
#endif
