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
    public static extern long CFDataGetLength(IntPtr data);

    [DllImport(Libraries.CoreFoundation)]
    public static extern IntPtr CFDataGetBytePtr(IntPtr data);

    [DllImport(Libraries.CoreFoundation)]
    public static extern bool CFNumberGetValue(IntPtr number, int theType, out long valuePtr);
    
    [DllImport(Libraries.CoreFoundation)]
    public static extern void CFRelease(IntPtr cf);
    
    [DllImport(Libraries.CoreFoundation)]
    public static unsafe extern bool CFStringGetCString(
        IntPtr theString,
        byte* buffer,
        long bufferSize,
        int encoding);

    private const int kCFStringEncodingUTF8 = 0x08000100;
    private const int kCFNumberSInt64Type = 4;
    
    public static void CFNumberGetValue(IntPtr number, out long value)
    {
        value = 0;
        
        CFNumberGetValue(
            number, 
            kCFNumberSInt64Type, 
            out value);
    }

    public static IntPtr CFStringCreate(string value) =>
        CFStringCreateWithCString(IntPtr.Zero, value, kCFStringEncodingUTF8);

    public static unsafe string? GetString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero)
            return null;

        Span<byte> buffer = stackalloc byte[1024];

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