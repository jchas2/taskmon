#if __APPLE__
using System.Text;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static class CFConvert
{
    private const int MaxPathBytes = 1024;

    public static unsafe nint CreateFileUrl(string path, bool isDirectory)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(path);

        fixed (byte* ptr = bytes) {
            return CoreFoundation.CFURLCreateFromFileSystemRepresentation(nint.Zero, ptr, bytes.Length, isDirectory);
        }
    }

    public static nint CreateString(string value) =>
        CoreFoundation.CFStringCreateWithCString(nint.Zero, value, CoreFoundation.kCFStringEncodingUTF8);

    public static double GetDouble(nint number)
    {
        CoreFoundation.CFNumberGetValue(number, CoreFoundation.kCFNumberDoubleType, out double value);
        return value;
    }

    public static long GetInt64(nint number)
    {
        CoreFoundation.CFNumberGetValue(number, CoreFoundation.kCFNumberSInt64Type, out long value);
        return value;
    }

    public static unsafe string? GetPath(nint url)
    {
        if (url == nint.Zero) {
            return null;
        }

        byte* buffer = stackalloc byte[MaxPathBytes];

        if (!CoreFoundation.CFURLGetFileSystemRepresentation(url, true, buffer, MaxPathBytes)) {
            return null;
        }

        int length = new ReadOnlySpan<byte>(buffer, MaxPathBytes).IndexOf((byte)0);

        return Encoding.UTF8.GetString(buffer, length >= 0 ? length : MaxPathBytes);
    }

    public static unsafe string? GetString(nint cfString)
    {
        if (cfString == nint.Zero) {
            return null;
        }

        // Sized from the string itself; a fixed buffer silently fails (returns null) on a long
        // value such as a deeply nested app bundle path.
        long maxBytes = CoreFoundation.CFStringGetMaximumSizeForEncoding(
            CoreFoundation.CFStringGetLength(cfString),
            CoreFoundation.kCFStringEncodingUTF8) + 1;

        if (maxBytes <= 0 || maxBytes > int.MaxValue) {
            return null;
        }

        Span<byte> buffer = maxBytes <= 1024
            ? stackalloc byte[1024]
            : new byte[maxBytes];

        fixed (byte* ptr = buffer) {
            if (CoreFoundation.CFStringGetCString(cfString, ptr, buffer.Length, CoreFoundation.kCFStringEncodingUTF8)) {
                int length = buffer.IndexOf<byte>(0);

                if (length >= 0) {
                    return Encoding.UTF8.GetString(buffer.Slice(0, length));
                }
            }
        }

        return null;
    }

    public static unsafe Dictionary<string, nint> ToDictionary(nint cfDict)
    {
        Dictionary<string, nint> result = new();

        if (cfDict == nint.Zero) {
            return result;
        }

        int count = (int)CoreFoundation.CFDictionaryGetCount(cfDict);

        if (count == 0) {
            return result;
        }

        Span<nint> keys   = stackalloc nint[count];
        Span<nint> values = stackalloc nint[count];

        fixed (nint* keysPtr   = keys)
        fixed (nint* valuesPtr = values) {
            CoreFoundation.CFDictionaryGetKeysAndValues((nint*)cfDict, keysPtr, valuesPtr);
        }

        for (int i = 0; i < count; i++) {
            if (GetString(keys[i]) is { } key) {
                result[key] = values[i];
            }
        }

        return result;
    }
}
#endif
