using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class CoreFoundation
{
    public const int   kCFStringEncodingUTF8         = 0x08000100;
    public const int   kCFNumberSInt64Type           = 4;
    public const int   kCFNumberDoubleType           = 13;
    public const ulong kCFPropertyListImmutable      = 0;
    public const long  kCFPropertyListXMLFormat_v1_0 = 100;

    [DllImport(Libraries.CoreFoundation)]
    public static unsafe extern IntPtr CFArrayCreate(IntPtr allocator, IntPtr* values, long numValues, IntPtr callBacks);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFArrayGetCount(IntPtr array);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFArrayGetTypeID();

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, long index);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFBooleanGetTypeID();

    [DllImport(Libraries.CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFBooleanGetValue(IntPtr boolean);

    [DllImport(Libraries.CoreFoundation)]
    public static unsafe extern IntPtr CFDataCreate(IntPtr allocator, byte* bytes, long length);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFDataGetBytePtr(IntPtr data);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFDataGetLength(IntPtr data);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFDataGetTypeID();

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFDictionaryCreateMutableCopy(IntPtr allocator, long capacity, IntPtr theDict);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFDictionaryGetCount(IntPtr dict);

    [DllImport(Libraries.CoreFoundation)]
    public static unsafe extern void CFDictionaryGetKeysAndValues(
        nint* dict,
        nint* keys,
        nint* values);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFDictionaryGetTypeID();

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFDictionaryGetValue(IntPtr dict, IntPtr key);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFGetRetainCount(IntPtr cf);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFGetTypeID(IntPtr cf);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFNumberGetTypeID();

    [DllImport(Libraries.CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, int theType, out long valuePtr);

    [DllImport(Libraries.CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, int theType, out double valuePtr);

    [DllImport(Libraries.CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberIsFloatType(IntPtr number);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFPropertyListCreateData(
        IntPtr allocator,
        IntPtr propertyList,
        long   format,
        ulong  options,
        IntPtr error);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFPropertyListCreateWithData(
        IntPtr allocator,
        IntPtr data,
        ulong  options,
        IntPtr format,
        IntPtr error);

    [DllImport(Libraries.CoreFoundation)]
    public static extern void CFRelease(IntPtr cf);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFRetain(IntPtr cf);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFStringCreateWithCString(
        IntPtr allocator,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string cStr,
        int    encoding);

    [DllImport(Libraries.CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static unsafe extern bool CFStringGetCString(
        IntPtr theString,
        byte*  buffer,
        long   bufferSize,
        int    encoding);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFStringGetLength(IntPtr theString);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFStringGetMaximumSizeForEncoding(long length, int encoding);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFStringGetTypeID();

    [DllImport(Libraries.CoreFoundation)]
    public static unsafe extern IntPtr CFURLCreateFromFileSystemRepresentation(
        IntPtr allocator,
        byte*  buffer,
        long   bufLen,
        [MarshalAs(UnmanagedType.U1)]
        bool   isDirectory);

    [DllImport(Libraries.CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static unsafe extern bool CFURLGetFileSystemRepresentation(
        IntPtr url,
        [MarshalAs(UnmanagedType.U1)]
        bool   resolveAgainstBase,
        byte*  buffer,
        long   maxBufLen);
}
