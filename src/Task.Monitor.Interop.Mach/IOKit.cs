using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class IOKit
{
    [DllImport(Libraries.IOKit)]
    public static extern uint IOIteratorNext(IntPtr iterator);

    [DllImport(Libraries.IOKit)]
    public static extern int IOObjectRelease(uint obj);

    [DllImport(Libraries.IOKit)]
    public static extern int IOObjectRelease(IntPtr obj);

    [DllImport(Libraries.IOKit)]
    public static extern int IOObjectRetain(uint obj);

    [DllImport(Libraries.IOKit)]
    public static extern uint IOObjectGetUserRetainCount(uint obj);
    
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
    public static extern int IOServiceGetMatchingServices(
        uint     masterPort,
        nint     matching,
        out nint iterator);
    
    [DllImport(Libraries.IOKit)]
    public static extern IntPtr IORegistryEntrySearchCFProperty(
        uint   entry,
        string plane,
        nint   key,
        nint   allocator,
        uint   options);

    [DllImport(Libraries.IOKit)]
    public static extern IntPtr IOServiceMatching(string name);

    [DllImport(Libraries.IOKit)]
    public static extern uint IOServiceGetMatchingService(uint masterPort, IntPtr matching);
}
