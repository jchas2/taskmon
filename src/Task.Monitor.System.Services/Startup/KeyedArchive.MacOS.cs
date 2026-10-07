#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Startup;

// Decodes an NSKeyedArchiver archive.
public static class KeyedArchive
{
    public const string ClassKey = "$class";

    private const string Archiver = "NSKeyedArchiver";
    private const string NullObject = "$null";
    private const int MaxDepth = 32;

    private static readonly DateTime ReferenceDate = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static Dictionary<string, object?>? Decode(object? archive)
    {
        if (archive is not IReadOnlyDictionary<string, object?> root ||
            root.GetValueOrDefault("$archiver") is not Archiver ||
            root.GetValueOrDefault("$objects") is not List<object?> objects ||
            root.GetValueOrDefault("$top") is not IReadOnlyDictionary<string, object?> top) {

            return null;
        }

        Decoder decoder = new(objects);
        Dictionary<string, object?> result = new(StringComparer.Ordinal);

        foreach ((string key, object? value) in top) {
            result[key] = decoder.Resolve(value, 0);
        }

        return result;
    }

    private sealed class Decoder(List<object?> objects)
    {
        private readonly HashSet<long> resolved = new();

        public object? Resolve(object? value, int depth)
        {
            if (depth > MaxDepth) {
                return null;
            }

            if (value is not PropertyListUid uid) {
                return value switch {
                    List<object?> list                          => list.Select(item => Resolve(item, depth + 1)).ToList(),
                    IReadOnlyDictionary<string, object?> fields => ResolveObject(fields, depth),
                    _                                           => value
                };
            }

            if (uid.Value < 0 || uid.Value >= objects.Count || !resolved.Add(uid.Value)) {
                return null;
            }

            try {
                object? target = objects[(int)uid.Value];

                return target is NullObject 
                    ? null 
                    : Resolve(target, depth + 1);
            }
            finally {
                resolved.Remove(uid.Value);
            }
        }

        private object? ResolveObject(IReadOnlyDictionary<string, object?> fields, int depth)
        {
            string? className = fields.GetValueOrDefault(ClassKey) is PropertyListUid classUid &&
                                classUid.Value >= 0 && classUid.Value < objects.Count &&
                                objects[(int)classUid.Value] is IReadOnlyDictionary<string, object?> classInfo
                ? classInfo.GetValueOrDefault("$classname") as string
                : null;

            switch (className) {
                case "NSDictionary":
                case "NSMutableDictionary":
                    return ResolveDictionary(fields, depth);

                case "NSArray":
                case "NSMutableArray":
                case "NSSet":
                case "NSMutableSet":
                case "NSOrderedSet":
                case "NSMutableOrderedSet":
                    return Resolve(fields.GetValueOrDefault("NS.objects"), depth + 1) as List<object?> ?? [];

                case "NSString":
                case "NSMutableString":
                    return Resolve(fields.GetValueOrDefault("NS.string"), depth + 1) as string;

                case "NSData":
                case "NSMutableData":
                    return Resolve(fields.GetValueOrDefault("NS.data"), depth + 1) as byte[];

                case "NSURL":
                    return ResolveUrl(fields, depth);

                case "NSUUID":
                    return fields.GetValueOrDefault("NS.uuidbytes") is byte[] { Length: 16 } bytes
                        ? new Guid(bytes, bigEndian: true).ToString("D").ToUpperInvariant()
                        : null;

                case "NSDate":
                    return fields.GetValueOrDefault("NS.time") is double seconds
                        ? ReferenceDate.AddSeconds(seconds)
                        : null;
            }

            Dictionary<string, object?> result = new(StringComparer.Ordinal);

            foreach ((string key, object? value) in fields) {
                if (key != ClassKey) {
                    result[key] = Resolve(value, depth + 1);
                }
            }

            if (className is not null) {
                result[ClassKey] = className;
            }

            return result;
        }

        private Dictionary<string, object?> ResolveDictionary(IReadOnlyDictionary<string, object?> fields, int depth)
        {
            Dictionary<string, object?> result = new(StringComparer.Ordinal);

            if (fields.GetValueOrDefault("NS.keys") is not List<object?> keys ||
                fields.GetValueOrDefault("NS.objects") is not List<object?> values) {

                return result;
            }

            for (int i = 0; i < Math.Min(keys.Count, values.Count); i++) {
                if (Resolve(keys[i], depth + 1) is string key) {
                    result[key] = Resolve(values[i], depth + 1);
                }
            }

            return result;
        }

        private string? ResolveUrl(IReadOnlyDictionary<string, object?> fields, int depth)
        {
            string? relative = Resolve(fields.GetValueOrDefault("NS.relative"), depth + 1) as string;
            string? baseUrl = Resolve(fields.GetValueOrDefault("NS.base"), depth + 1) as string;

            if (relative is null || baseUrl is null) {
                return relative;
            }

            return Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri) &&
                   Uri.TryCreate(baseUri, relative, out Uri? combined)
                ? combined.AbsoluteUri
                : relative;
        }
    }
}
#endif
