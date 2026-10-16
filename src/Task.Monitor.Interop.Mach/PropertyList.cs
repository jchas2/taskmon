using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Task.Monitor.Interop.Mach;

public readonly record struct PropertyListUid(long Value);

public static class PropertyList
{
    private const ulong  kCFPropertyListImmutable      = 0;
    private const long   kCFPropertyListXMLFormat_v1_0 = 100;
    private const string UidKey                        = "CF$UID";
    private const int    MaxDepth                      = 32;

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

    public static object? Parse(ReadOnlySpan<byte> bytes) =>
        WithPropertyList(bytes, ToManaged);

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

    public static object? ParseKeyedArchive(ReadOnlySpan<byte> bytes) =>
        WithPropertyList(bytes, plist => {
            using CFScope xml = new(CoreFoundation.CFPropertyListCreateData(
                nint.Zero,
                plist,
                kCFPropertyListXMLFormat_v1_0,
                0,
                nint.Zero));

            return xml.IsNull ? null : FromXml(ToBytes(xml));
        });

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
            kCFPropertyListImmutable,
            nint.Zero,
            nint.Zero));

        return plist.IsNull ? null : convert(plist);
    }

    // Converts a borrowed CF property list object; the caller keeps ownership.
    public static object? ToManaged(nint cf) => ToManaged(cf, 0);

    private static object? ToManaged(nint cf, int depth)
    {
        if (cf == nint.Zero || depth > MaxDepth) {
            return null;
        }

        long typeId = CoreFoundation.CFGetTypeID(cf);

        if (typeId == CoreFoundation.CFStringGetTypeID()) {
            return CoreFoundation.GetString(cf);
        }

        if (typeId == CoreFoundation.CFBooleanGetTypeID()) {
            return CoreFoundation.CFBooleanGetValue(cf);
        }

        if (typeId == CoreFoundation.CFNumberGetTypeID()) {
            if (CoreFoundation.CFNumberIsFloatType(cf)) {
                CoreFoundation.CFNumberGetValue(cf, out double real);
                return real;
            }

            CoreFoundation.CFNumberGetValue(cf, out long integer);
            return integer;
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
            if (CoreFoundation.CFGetTypeID(keys[i]) != CoreFoundation.CFStringGetTypeID()) {
                continue;
            }

            string? key = CoreFoundation.GetString(keys[i]);

            if (key != null) {
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

    private static byte[]? FromBase64(string text)
    {
        try {
            return Convert.FromBase64String(string.Concat(text.Where(c => !char.IsWhiteSpace(c))));
        }
        catch (FormatException) {
            return null;
        }
    }

    private static unsafe byte[] ToBytes(nint cfData)
    {
        long length  = CoreFoundation.CFDataGetLength(cfData);
        nint bytes   = CoreFoundation.CFDataGetBytePtr(cfData);

        return length <= 0 || bytes == nint.Zero
            ? []
            : new ReadOnlySpan<byte>((void*)bytes, (int)length).ToArray();
    }
}
