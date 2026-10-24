using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class IOKit
{
    public static unsafe Dictionary<string, object?>? GetLoadedKextInfo(IReadOnlyList<string> infoKeys)
    {
        nint[] keys = new nint[infoKeys.Count];

        try {
            for (int i = 0; i < keys.Length; i++) {
                keys[i] = CoreFoundation.CFStringCreate(infoKeys[i]);
            }

            fixed (nint* keysPtr = keys) {
                using CFScope keyArray = new(CoreFoundation.CFArrayCreate(nint.Zero, keysPtr, keys.Length, nint.Zero));
                using CFScope info     = new(KextManagerCopyLoadedKextInfo(nint.Zero, keyArray));

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

    [DllImport(Libraries.IOKit)]
    public static extern unsafe int IOConnectCallStructMethod(
        uint   connection,
        uint   selector,
        void*  input,
        nuint  inputSize,
        void*  output,
        nuint* outputSize);

    [DllImport(Libraries.IOKit)]
    public static extern uint IOIteratorNext(IntPtr iterator);

    [DllImport(Libraries.IOKit)]
    public static extern uint IOObjectGetUserRetainCount(uint obj);

    [DllImport(Libraries.IOKit)]
    public static extern int IOObjectRelease(uint obj);

    [DllImport(Libraries.IOKit)]
    public static extern int IOObjectRelease(IntPtr obj);

    [DllImport(Libraries.IOKit)]
    public static extern int IOObjectRetain(uint obj);

    [DllImport(Libraries.IOKit)]
    public static extern int IORegistryEntryCreateCFProperties(
        uint     entry, 
        out nint properties, 
        nint     allocator, 
        uint     options);
    
    [DllImport(Libraries.IOKit)]
    public static extern int IORegistryEntryGetChildIterator(
        uint     entry,
        string   plane,
        ref nint iterator);

    [DllImport(Libraries.IOKit)]
    public static extern int IORegistryEntryGetName(uint entry, byte[] name);

    [DllImport(Libraries.IOKit)]
    public static extern IntPtr IORegistryEntrySearchCFProperty(
        uint   entry,
        string plane,
        nint   key,
        nint   allocator,
        uint   options);

    [DllImport(Libraries.IOKit)]
    public static extern int IOServiceClose(uint connection);

    [DllImport(Libraries.IOKit)]
    public static extern uint IOServiceGetMatchingService(uint masterPort, IntPtr matching);

    [DllImport(Libraries.IOKit)]
    public static extern int IOServiceGetMatchingServices(
        uint     masterPort,
        nint     matching,
        out nint iterator);

    [DllImport(Libraries.IOKit)]
    public static extern IntPtr IOServiceMatching(string name);

    [DllImport(Libraries.IOKit)]
    public static extern IntPtr IOServiceNameMatching(string name);

    [DllImport(Libraries.IOKit)]
    public static extern int IOServiceOpen(
        uint     service,
        uint     owningTask,
        uint     type,
        out uint connection);

    [DllImport(Libraries.IOKit)]
    private static extern nint KextManagerCopyLoadedKextInfo(nint kextIdentifiers, nint infoKeys);
}
