#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static class MetalDevice
{
    public static ulong? GetRecommendedMaxWorkingSetSize()
    {
        nint device = Metal.MTLCreateSystemDefaultDevice();

        if (device == nint.Zero) {
            return null;
        }

        try {
            ulong size = ObjCRuntime.objc_msgSend_UInt64(device, ObjCRuntime.sel_registerName(Metal.RecommendedMaxWorkingSetSizeSelector));

            return size > 0 ? size : null;
        }
        finally {
            CoreFoundation.CFRelease(device);
        }
    }
}
#endif
