using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class CoreFoundation
{
    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFArrayGetCount(IntPtr array);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, long index);
    
    [DllImport(Libraries.CoreFoundation)]                                                                                                                         
    public static extern long CFArrayGetTypeID();                                                                                                                 
   
    [DllImport(Libraries.CoreFoundation)]
    public static unsafe extern void CFDictionaryGetKeysAndValues(
        nint* dict,
        nint* keys,
        nint* values);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFDictionaryGetCount(IntPtr dict);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFDictionaryCreateMutableCopy(IntPtr allocator, long capacity, IntPtr theDict);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFStringCreateWithCString(
        IntPtr allocator,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cStr,
        int encoding);

    [DllImport(Libraries.CoreFoundation)]                                                                                                                         
    public static extern long CFDictionaryGetTypeID();

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFGetTypeID(IntPtr cf);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFDictionaryGetValue(IntPtr dict, IntPtr key);

    [DllImport(Libraries.CoreFoundation)]
    public static unsafe extern IntPtr CFDataCreate(IntPtr allocator, byte* bytes, long length);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFDataGetTypeID();

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFDataGetLength(IntPtr data);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFDataGetBytePtr(IntPtr data);

    [DllImport(Libraries.CoreFoundation)]
    public static extern bool CFNumberGetValue(IntPtr number, int theType, out long valuePtr);

    [DllImport(Libraries.CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, int theType, out double valuePtr);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFNumberGetTypeID();

    [DllImport(Libraries.CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberIsFloatType(IntPtr number);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFBooleanGetTypeID();

    [DllImport(Libraries.CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFBooleanGetValue(IntPtr boolean);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFStringGetTypeID();

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFStringGetLength(IntPtr theString);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFStringGetMaximumSizeForEncoding(long length, int encoding);

    // Reads XML and binary (bplist00) property lists alike. format and error are optional out
    // pointers, passed as zero when not wanted.
    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFPropertyListCreateWithData(
        IntPtr allocator,
        IntPtr data,
        ulong options,
        IntPtr format,
        IntPtr error);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFPropertyListCreateData(
        IntPtr allocator,
        IntPtr propertyList,
        long format,
        ulong options,
        IntPtr error);

    [DllImport(Libraries.CoreFoundation)]
    public static unsafe extern IntPtr CFURLCreateFromFileSystemRepresentation(
        IntPtr allocator,
        byte* buffer,
        long bufLen,
        [MarshalAs(UnmanagedType.U1)] bool isDirectory);

    [DllImport(Libraries.CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static unsafe extern bool CFURLGetFileSystemRepresentation(
        IntPtr url,
        [MarshalAs(UnmanagedType.U1)] bool resolveAgainstBase,
        byte* buffer,
        long maxBufLen);
    
    [DllImport(Libraries.CoreFoundation)]
    public static extern void CFRelease(IntPtr cf);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFRetain(IntPtr cf);

    [DllImport(Libraries.CoreFoundation)]
    public static extern long CFGetRetainCount(IntPtr cf);
    
    [DllImport(Libraries.CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static unsafe extern bool CFStringGetCString(
        IntPtr theString,
        byte* buffer,
        long bufferSize,
        int encoding);

    private const int kCFStringEncodingUTF8 = 0x08000100;
    private const int kCFNumberSInt64Type = 4;
    private const int kCFNumberDoubleType = 13;
    private const int MaxPathBytes = 1024; // PATH_MAX
    
    public static void CFNumberGetValue(IntPtr number, out long value)
    {
        value = 0;
        
        CFNumberGetValue(
            number, 
            kCFNumberSInt64Type, 
            out value);
    }

    public static void CFNumberGetValue(IntPtr number, out double value) =>
        CFNumberGetValue(number, kCFNumberDoubleType, out value);

    public static IntPtr CFStringCreate(string value) =>
        CFStringCreateWithCString(IntPtr.Zero, value, kCFStringEncodingUTF8);

    public static unsafe string? GetString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero)
            return null;

        // Sized from the string itself; a fixed buffer silently fails (returns null) on a long
        // value such as a deeply nested app bundle path.
        long maxBytes = CFStringGetMaximumSizeForEncoding(CFStringGetLength(cfString), kCFStringEncodingUTF8) + 1;

        if (maxBytes <= 0 || maxBytes > int.MaxValue) {
            return null;
        }

        Span<byte> buffer = maxBytes <= 1024
            ? stackalloc byte[1024]
            : new byte[maxBytes];

        fixed (byte* ptr = buffer) {
            
            if (CFStringGetCString(
                cfString, 
                ptr, 
                buffer.Length, 
                kCFStringEncodingUTF8)) {
                
                int length = buffer.IndexOf<byte>(0);

                if (length >= 0) {
                    return System.Text.Encoding.UTF8.GetString(buffer.Slice(0, length));
                }
            }
        }

        return null;
    }

    // CFURLRef for a file system path. The caller releases the returned URL.
    public static unsafe IntPtr CFURLCreateFromPath(string path, bool isDirectory)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(path);

        fixed (byte* ptr = bytes) {
            return CFURLCreateFromFileSystemRepresentation(IntPtr.Zero, ptr, bytes.Length, isDirectory);
        }
    }

    public static unsafe string? GetPath(IntPtr url)
    {
        if (url == IntPtr.Zero) {
            return null;
        }

        byte* buffer = stackalloc byte[MaxPathBytes];

        if (!CFURLGetFileSystemRepresentation(url, true, buffer, MaxPathBytes)) {
            return null;
        }

        int length = new ReadOnlySpan<byte>(buffer, MaxPathBytes).IndexOf((byte)0);

        return System.Text.Encoding.UTF8.GetString(buffer, length >= 0 ? length : MaxPathBytes);
    }
    
    public static unsafe Dictionary<string, IntPtr> ToDictionary(IntPtr cfDict)
    {
        Dictionary<string, IntPtr> result = new();

        if (cfDict == IntPtr.Zero) {
            return result;
        }

        int count = (int)CFDictionaryGetCount(cfDict);
        
        if (count == 0) {
            return result;
        }

        Span<nint> keys = stackalloc nint[count];
        Span<nint> values = stackalloc nint[count];
        
        fixed (nint* keysPtr = keys)
        fixed (nint* valuesPtr = values) {

            CFDictionaryGetKeysAndValues(
                (nint*)cfDict, 
                keysPtr, 
                valuesPtr);

            nint* nextKey = keysPtr;
            nint* nextVal = valuesPtr;

            for (int i = 0; i < count; i++)
            {
                string? key = GetString(*nextKey);
                
                if (key != null) {
                    result[key] = *nextVal;
                }

                nextKey++;
                nextVal++;
            }
        }

        return result;
    }
}