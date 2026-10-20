using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// The Objective-C runtime's C API (objc/runtime.h, objc/message.h), for the few Foundation-level
// APIs that have no C equivalent. 
public static class ObjCRuntime
{
    [DllImport(Libraries.ObjC)]
    public static extern void objc_autoreleasePoolPop(nint pool);

    [DllImport(Libraries.ObjC)]
    public static extern nint objc_autoreleasePoolPush();

    [DllImport(Libraries.ObjC)]
    public static extern nint objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Libraries.ObjC, EntryPoint = "objc_msgSend")]
    public static extern long objc_msgSend_Int64_IntPtr(nint receiver, nint selector, nint argument);

    [DllImport(Libraries.ObjC)]
    public static extern nint sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
}
