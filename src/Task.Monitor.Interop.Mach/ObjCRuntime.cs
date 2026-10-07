using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// The Objective-C runtime's C API (objc/runtime.h, objc/message.h), for the few Foundation-level
// APIs that have no C equivalent. objc_msgSend is bound once per call signature; on arm64 it must
// be called with exactly the prototype of the method it dispatches to.
public static class ObjCRuntime
{
    [DllImport(Libraries.ObjC)]
    public static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Libraries.ObjC)]
    public static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    // (NSInteger)method:(id)argument
    [DllImport(Libraries.ObjC, EntryPoint = "objc_msgSend")]
    public static extern long objc_msgSend_Int64_IntPtr(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(Libraries.ObjC)]
    public static extern IntPtr objc_autoreleasePoolPush();

    [DllImport(Libraries.ObjC)]
    public static extern void objc_autoreleasePoolPop(IntPtr pool);
}
