#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static class Spotlight
{
    // Spotlight already knows a bundle's total size, so reading it avoids walking every file in it.
    public static long? GetLogicalSize(string path)
    {
        using CFScope cfPath = new(CFConvert.CreateString(path));

        if (cfPath.IsNull) {
            return null;
        }

        using CFScope item = new(Metadata.MDItemCreate(nint.Zero, cfPath));

        if (item.IsNull) {
            return null;
        }

        using CFScope name  = new(CFConvert.CreateString(Metadata.kMDItemLogicalSize));
        using CFScope value = new(Metadata.MDItemCopyAttribute(item, name));

        if (value.IsNull || CoreFoundation.CFGetTypeID(value) != CoreFoundation.CFNumberGetTypeID()) {
            return null;
        }

        return CFConvert.GetInt64(value);
    }
}
#endif
