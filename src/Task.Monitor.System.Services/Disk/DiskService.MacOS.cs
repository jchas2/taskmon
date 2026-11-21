#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
    private const string BlockStorageDriverClass = "IOBlockStorageDriver";
    private const string VirtualInterconnect     = "Virtual Interface";

    internal static bool IsVirtualInterconnect(string? physicalInterconnect) =>
        string.Equals(physicalInterconnect, VirtualInterconnect, StringComparison.Ordinal);

    // Parses "disk0", "disk12" -> 0, 12; otherwise  -1.
    private static int ParseDiskIndex(string? bsdName) =>
        !string.IsNullOrEmpty(bsdName) &&
        bsdName.StartsWith("disk", StringComparison.Ordinal) &&
        int.TryParse(bsdName.AsSpan(4), out int index)
            ? index
            : -1;

    private static string? ReadBsdName(uint entry) =>
        IOKit.SearchProperty(entry, "BSD Name", IOKit.IterateRecursively) as string;

    // A dictionary property of the entry or the nearest parent that has it.
    private static Dictionary<string, object?>? ReadCharacteristics(uint entry, string key) =>
        IOKit.SearchProperty(entry, key, IOKit.IterateRecursively | IOKit.IterateParents) as Dictionary<string, object?>;

    private static string? ReadPhysicalInterconnect(uint entry) =>
        ReadCharacteristics(entry, "Protocol Characteristics") is { } protocol
            ? TrimmedString(protocol, "Physical Interconnect")
            : null;

    private static string? TrimmedString(Dictionary<string, object?> properties, string key) =>
        (properties.GetValueOrDefault(key) as string)?.Trim() is { Length: > 0 } text
            ? text
            : null;
}
#endif
