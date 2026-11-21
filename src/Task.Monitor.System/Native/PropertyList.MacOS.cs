#if __APPLE__
using System.Buffers;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32.SafeHandles;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static class PropertyList
{
    private const string UidKey   = "CF$UID";
    private const int    MaxDepth = 32;

    private static Dictionary<string, object?>? ExtractStrings(
        nint                   plist,
        IReadOnlyList<string>  keys,
        IReadOnlyList<string>? tables)
    {
        nint dictionary = IsDictionary(plist) ? plist : nint.Zero;

        if (dictionary != nint.Zero && tables is not null) {
            dictionary = FindTable(dictionary, tables);
        }

        if (dictionary == nint.Zero) {
            return null;
        }

        Dictionary<string, object?> strings = new(keys.Count, StringComparer.Ordinal);

        foreach (string key in keys) {
            nint value = GetValue(dictionary, key);

            if (value != nint.Zero &&
                CoreFoundation.CFGetTypeID(value) == CoreFoundation.CFStringGetTypeID() &&
                CFConvert.GetString(value) is { } text) {

                strings[key] = text;
            }
        }

        return strings;
    }

    private static nint FindTable(nint dictionary, IReadOnlyList<string> tables)
    {
        foreach (string table in tables) {
            nint candidate = GetValue(dictionary, table);

            if (IsDictionary(candidate)) {
                return candidate;
            }
        }

        return nint.Zero;
    }

    private static byte[]? FromBase64(string text)
    {
        try {
            return Convert.FromBase64String(string.Concat(text.Where(c => !char.IsWhiteSpace(c))));
        }
        catch (FormatException) {
            return null;
        }
    }

    private static object? FromXml(byte[] xml)
    {
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Ignore };

        try {
            using XmlReader reader = XmlReader.Create(new MemoryStream(xml), settings);

            XElement? value = XDocument.Load(reader).Root?.Elements().FirstOrDefault();

            return value is null ? null : FromXml(value, 0);
        }
        catch (XmlException) {
            return null;
        }
    }

    private static object? FromXml(XElement element, int depth)
    {
        if (depth > MaxDepth) {
            return null;
        }

        return element.Name.LocalName switch {
            "dict"    => FromXmlDictionary(element, depth),
            "array"   => element.Elements().Select(child => FromXml(child, depth + 1)).ToList(),
            "string"  => element.Value,
            "integer" => long.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)
                ? integer
                : null,
            "real"    => double.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double real)
                ? real
                : null,
            "true"    => true,
            "false"   => false,
            "data"    => FromBase64(element.Value),
            _         => null
        };
    }

    private static object FromXmlDictionary(XElement element, int depth)
    {
        List<XElement> children = element.Elements().ToList();

        if (children is [{ Name.LocalName: "key", Value: UidKey }, { Name.LocalName: "integer" } uid] &&
            long.TryParse(uid.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long index)) {

            return new PropertyListUid(index);
        }

        Dictionary<string, object?> result = new(children.Count / 2, StringComparer.Ordinal);

        for (int i = 0; i + 1 < children.Count; i += 2) {
            if (children[i].Name.LocalName == "key") {
                result[children[i].Value] = FromXml(children[i + 1], depth + 1);
            }
        }

        return result;
    }

    private static nint GetValue(nint dictionary, string key)
    {
        using CFScope cfKey = new(CFConvert.CreateString(key));

        return cfKey.IsNull
            ? nint.Zero
            : CoreFoundation.CFDictionaryGetValue(dictionary, cfKey);
    }

    private static bool IsDictionary(nint cf) =>
        cf != nint.Zero && CoreFoundation.CFGetTypeID(cf) == CoreFoundation.CFDictionaryGetTypeID();

    public static object? Parse(ReadOnlySpan<byte> bytes) =>
        WithPropertyList(bytes, ToManaged);

    public static object? ParseKeyedArchive(ReadOnlySpan<byte> bytes) =>
        WithPropertyList(bytes, plist => {
            using CFScope xml = new(CoreFoundation.CFPropertyListCreateData(
                nint.Zero,
                plist,
                CoreFoundation.kCFPropertyListXMLFormat_v1_0,
                0,
                nint.Zero));

            return xml.IsNull ? null : FromXml(ToBytes(xml));
        });

    public static object? ReadFile(string path)
    {
        byte[] bytes;

        try {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return null;
        }

        return Parse(bytes);
    }

    public static object? ReadKeyedArchiveFile(string path)
    {
        byte[] bytes;

        try {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return null;
        }

        return ParseKeyedArchive(bytes);
    }

    // A localized InfoPlist.loctable holds every language, yet a caller wants a few keys from one,
    // so only the named strings are converted to managed values.
    public static Dictionary<string, object?>? ReadStrings(
        string                 path,
        IReadOnlyList<string>  keys,
        IReadOnlyList<string>? tables = null)
    {
        if (!File.Exists(path)) {
            return null;
        }

        byte[]? buffer = null;

        try {
            using SafeFileHandle handle = File.OpenHandle(path);

            long length = RandomAccess.GetLength(handle);

            if (length is <= 0 or > int.MaxValue) {
                return null;
            }

            // Pooled, so reading a few hundred small files allocates no new arrays.
            buffer = ArrayPool<byte>.Shared.Rent((int)length);

            int read = 0;

            while (read < length) {
                int count = RandomAccess.Read(handle, buffer.AsSpan(read, (int)length - read), read);

                if (count == 0) {
                    break;
                }

                read += count;
            }

            return WithPropertyList(buffer.AsSpan(0, read), plist => ExtractStrings(plist, keys, tables))
                as Dictionary<string, object?>;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return null;
        }
        finally {
            if (buffer is not null) {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    private static unsafe byte[] ToBytes(nint cfData)
    {
        long length = CoreFoundation.CFDataGetLength(cfData);
        nint bytes  = CoreFoundation.CFDataGetBytePtr(cfData);

        return length <= 0 || bytes == nint.Zero
            ? []
            : new ReadOnlySpan<byte>((void*)bytes, (int)length).ToArray();
    }

    private static unsafe Dictionary<string, object?> ToDictionary(nint cfDict, int depth)
    {
        int count = (int)CoreFoundation.CFDictionaryGetCount(cfDict);
        Dictionary<string, object?> result = new(count, StringComparer.Ordinal);

        if (count == 0) {
            return result;
        }

        nint[] keys   = new nint[count];
        nint[] values = new nint[count];

        fixed (nint* keysPtr   = keys)
        fixed (nint* valuesPtr = values) {
            CoreFoundation.CFDictionaryGetKeysAndValues((nint*)cfDict, keysPtr, valuesPtr);
        }

        for (int i = 0; i < count; i++) {
            if (CoreFoundation.CFGetTypeID(keys[i]) == CoreFoundation.CFStringGetTypeID() &&
                CFConvert.GetString(keys[i]) is { } key) {

                result[key] = ToManaged(values[i], depth + 1);
            }
        }

        return result;
    }

    private static List<object?> ToList(nint cfArray, int depth)
    {
        long count           = CoreFoundation.CFArrayGetCount(cfArray);
        List<object?> result = new((int)count);

        for (long i = 0; i < count; i++) {
            result.Add(ToManaged(CoreFoundation.CFArrayGetValueAtIndex(cfArray, i), depth + 1));
        }

        return result;
    }

    public static object? ToManaged(nint cf) => ToManaged(cf, 0);

    private static object? ToManaged(nint cf, int depth)
    {
        if (cf == nint.Zero || depth > MaxDepth) {
            return null;
        }

        long typeId = CoreFoundation.CFGetTypeID(cf);

        if (typeId == CoreFoundation.CFStringGetTypeID()) {
            return CFConvert.GetString(cf);
        }

        if (typeId == CoreFoundation.CFBooleanGetTypeID()) {
            return CoreFoundation.CFBooleanGetValue(cf);
        }

        if (typeId == CoreFoundation.CFNumberGetTypeID()) {
            if (CoreFoundation.CFNumberIsFloatType(cf)) {
                return CFConvert.GetDouble(cf);
            }

            return CFConvert.GetInt64(cf);
        }

        if (typeId == CoreFoundation.CFDictionaryGetTypeID()) {
            return ToDictionary(cf, depth);
        }

        if (typeId == CoreFoundation.CFArrayGetTypeID()) {
            return ToList(cf, depth);
        }

        if (typeId == CoreFoundation.CFDataGetTypeID()) {
            return ToBytes(cf);
        }

        return null;
    }

    private static unsafe object? WithPropertyList(ReadOnlySpan<byte> bytes, Func<nint, object?> convert)
    {
        if (bytes.IsEmpty) {
            return null;
        }

        nint dataRef;

        fixed (byte* ptr = bytes) {
            dataRef = CoreFoundation.CFDataCreate(nint.Zero, ptr, bytes.Length);
        }

        using CFScope data = new(dataRef);

        if (data.IsNull) {
            return null;
        }

        using CFScope plist = new(CoreFoundation.CFPropertyListCreateWithData(
            nint.Zero,
            data,
            CoreFoundation.kCFPropertyListImmutable,
            nint.Zero,
            nint.Zero));

        return plist.IsNull ? null : convert(plist);
    }
}
#endif
