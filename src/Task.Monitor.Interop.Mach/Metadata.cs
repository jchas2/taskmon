using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// Spotlight (Metadata framework) attributes, read from the Spotlight index rather than the disk.
public static class Metadata
{
    private const string kMDItemLogicalSize = "kMDItemLogicalSize";

    // The logical size Spotlight records for an item - for a bundle, the total of every file in it.
    // Null when Spotlight hasn't indexed the item: indexing is off, or the item is too new.
    public static long? GetLogicalSize(string path)
    {
        using CFScope cfPath = new(CoreFoundation.CFStringCreate(path));

        if (cfPath.IsNull) {
            return null;
        }

        using CFScope item = new(MDItemCreate(nint.Zero, cfPath));

        if (item.IsNull) {
            return null;
        }

        using CFScope name  = new(CoreFoundation.CFStringCreate(kMDItemLogicalSize));
        using CFScope value = new(MDItemCopyAttribute(item, name));

        if (value.IsNull || CoreFoundation.CFGetTypeID(value) != CoreFoundation.CFNumberGetTypeID()) {
            return null;
        }

        CoreFoundation.CFNumberGetValue(value, out long bytes);

        return bytes;
    }

    [DllImport(Libraries.CoreServices)]
    private static extern nint MDItemCopyAttribute(nint item, nint name);

    [DllImport(Libraries.CoreServices)]
    private static extern nint MDItemCreate(nint allocator, nint path);
}
