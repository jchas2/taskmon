using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Task.Monitor.Interop.Mach;

// A keyed archive object reference (CF$UID): an index into an NSKeyedArchiver's $objects.
public readonly record struct PropertyListUid(long Value);

// Property lists (XML or binary bplist00) materialised as managed values via CoreFoundation:
//   dict -> Dictionary<string, object?>, array -> List<object?>, string -> string,
//   boolean -> bool, integer -> long, real -> double, data -> byte[].
// Anything else (dates, non-string dictionary keys) is dropped as null.
public static class PropertyList
{
    private const ulong kCFPropertyListImmutable = 0;
    private const long kCFPropertyListXMLFormat_v1_0 = 100;
    private const string UidKey = "CF$UID";
    private const int MaxDepth = 32;

    // Null when the file can't be read (e.g. a root-only LaunchDaemon) or isn't a property list.
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

    // As ReadFile/Parse, but keeping NSKeyedArchiver object references as PropertyListUid. CF has
    // no public accessor for those, so the list is re-encoded as XML, where CF writes each one as
    // <dict><key>CF$UID</key><integer>n</integer></dict>, and that is read instead.
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
                IntPtr.Zero,
                plist,
                kCFPropertyListXMLFormat_v1_0,
                0,
                IntPtr.Zero));

            return xml.IsNull ? null : FromXml(ToBytes(xml));
        });

    private static unsafe object? WithPropertyList(ReadOnlySpan<byte> bytes, Func<IntPtr, object?> convert)
    {
        if (bytes.IsEmpty) {
            return null;
        }

        IntPtr dataRef;

        fixed (byte* ptr = bytes) {
            dataRef = CoreFoundation.CFDataCreate(IntPtr.Zero, ptr, bytes.Length);
        }

        using CFScope data = new(dataRef);

        if (data.IsNull) {
            return null;
        }

        using CFScope plist = new(CoreFoundation.CFPropertyListCreateWithData(
            IntPtr.Zero,
            data,
            kCFPropertyListImmutable,
            IntPtr.Zero,
            IntPtr.Zero));

        return plist.IsNull ? null : convert(plist);
    }

    // Converts a borrowed CF property list object; the caller keeps ownership of it.
    public static object? ToManaged(IntPtr cf) => ToManaged(cf, 0);

    private static object? ToManaged(IntPtr cf, int depth)
    {
        if (cf == IntPtr.Zero || depth > MaxDepth) {
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

    private static unsafe Dictionary<string, object?> ToDictionary(IntPtr cfDict, int depth)
    {
        int count = (int)CoreFoundation.CFDictionaryGetCount(cfDict);
        Dictionary<string, object?> result = new(count, StringComparer.Ordinal);

        if (count == 0) {
            return result;
        }

        nint[] keys = new nint[count];
        nint[] values = new nint[count];

        fixed (nint* keysPtr = keys)
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

    private static List<object?> ToList(IntPtr cfArray, int depth)
    {
        long count = CoreFoundation.CFArrayGetCount(cfArray);
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

    private static unsafe byte[] ToBytes(IntPtr cfData)
    {
        long length = CoreFoundation.CFDataGetLength(cfData);
        IntPtr bytes = CoreFoundation.CFDataGetBytePtr(cfData);

        return length <= 0 || bytes == IntPtr.Zero
            ? []
            : new ReadOnlySpan<byte>((void*)bytes, (int)length).ToArray();
    }
}
