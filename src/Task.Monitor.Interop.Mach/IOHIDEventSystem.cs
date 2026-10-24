using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// Private IOKit API that serves the SoC, battery and NAND temperature sensors.
public static class IOHIDEventSystem
{
    public const int  TemperatureEventType  = 15;
    public const int  TemperatureUsage      = 5;
    public const long VendorUsagePage       = 0xff00;
    public const int  TemperatureLevelField = TemperatureEventType << 16;

    [DllImport(Libraries.IOKit)]
    public static extern double IOHIDEventGetFloatValue(nint hidEvent, int field);

    [DllImport(Libraries.IOKit)]
    public static extern nint IOHIDEventSystemClientCopyServices(nint client);

    [DllImport(Libraries.IOKit)]
    public static extern nint IOHIDEventSystemClientCreate(nint allocator);

    [DllImport(Libraries.IOKit)]
    public static extern nint IOHIDServiceClientCopyEvent(nint service, long type, int options, long timestamp);

    [DllImport(Libraries.IOKit)]
    public static extern nint IOHIDServiceClientCopyProperty(nint service, nint key);
}
