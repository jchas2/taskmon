using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class Metal
{
    [DllImport(Libraries.Metal)]
    private static extern nint MTLCreateSystemDefaultDevice();

    public static ulong? RecommendedMaxWorkingSetSize()
    {
        nint device = MTLCreateSystemDefaultDevice();

        if (device == nint.Zero) {
            return null;
        }

        try {
            ulong size = ObjCRuntime.objc_msgSend_UInt64(device, ObjCRuntime.sel_registerName("recommendedMaxWorkingSetSize"));

            return size > 0 ? size : null;
        }
        finally {
            CoreFoundation.CFRelease(device);
        }
    }
}
