#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static class IOKitProperties
{
    public static unsafe Dictionary<string, object?>? GetLoadedKextInfo(IReadOnlyList<string> infoKeys)
    {
        nint[] keys = new nint[infoKeys.Count];

        try {
            for (int i = 0; i < keys.Length; i++) {
                keys[i] = CFConvert.CreateString(infoKeys[i]);
            }

            fixed (nint* keysPtr = keys) {
                using CFScope keyArray = new(CoreFoundation.CFArrayCreate(nint.Zero, keysPtr, keys.Length, nint.Zero));
                using CFScope info     = new(IOKit.KextManagerCopyLoadedKextInfo(nint.Zero, keyArray));

                return PropertyList.ToManaged(info) as Dictionary<string, object?>;
            }
        }
        finally {
            foreach (nint key in keys) {
                if (key != nint.Zero) {
                    CoreFoundation.CFRelease(key);
                }
            }
        }
    }

    public static object? SearchProperty(uint entry, string key, uint options = 0)
    {
        using CFScope name  = new(CFConvert.CreateString(key));
        using CFScope value = new(IOKit.IORegistryEntrySearchCFProperty(entry, IOKit.IOServicePlane, name, nint.Zero, options));

        return PropertyList.ToManaged(value);
    }
}
#endif
