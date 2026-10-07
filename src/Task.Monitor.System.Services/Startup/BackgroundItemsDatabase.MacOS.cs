#if __APPLE__
using System.Text.RegularExpressions;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Startup;

public sealed partial class BackgroundItemsDatabase
{
    public const string DirectoryPath = "/private/var/db/com.apple.backgroundtaskmanagement";

    private readonly IReadOnlyDictionary<string, List<BackgroundItemRecord>> recordsByUser;

    private BackgroundItemsDatabase(IReadOnlyDictionary<string, List<BackgroundItemRecord>> recordsByUser) =>
        this.recordsByUser = recordsByUser;

    public IReadOnlyList<BackgroundItemRecord> RecordsFor(string? userUuid) =>
        userUuid is not null && recordsByUser.TryGetValue(userUuid, out List<BackgroundItemRecord>? records)
            ? records
            : [];

    public static BackgroundItemsDatabase? Read()
    {
        string? path = FindNewestDatabase();

        if (path is null) {
            return null;
        }

        BackgroundItemsDatabase? database = Parse(PropertyList.ReadKeyedArchiveFile(path));

        TraceEx.WriteLineOnce(
            $"{nameof(BackgroundItemsDatabase)} {path}",
            database is null
                ? $"{path} is not in a recognised format; falling back to per-session APIs"
                : $"Read {path}");

        return database;
    }

    // archive: the output of PropertyList.ParseKeyedArchive.
    public static BackgroundItemsDatabase? Parse(object? archive)
    {
        if (KeyedArchive.Decode(archive) is not { } top ||
            FindItemsByUser(top) is not { } itemsByUser) {

            return null;
        }

        Dictionary<string, List<BackgroundItemRecord>> recordsByUser = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string userUuid, object? items) in itemsByUser) {
            if (items is not List<object?> list) {
                return null;
            }

            List<BackgroundItemRecord> records = new(list.Count);

            foreach (object? item in list) {
                if (ParseRecord(item) is { } record) {
                    records.Add(record);
                }
            }

            recordsByUser[userUuid] = records;
        }

        return new BackgroundItemsDatabase(recordsByUser);
    }

    private static Dictionary<string, object?>? FindItemsByUser(Dictionary<string, object?> top)
    {
        IEnumerable<object?> candidates = top.TryGetValue("store", out object? store)
            ? [store]
            : top.Values;

        return candidates
            .OfType<Dictionary<string, object?>>()
            .Select(candidate => candidate.GetValueOrDefault("itemsByUserIdentifier") as Dictionary<string, object?>)
            .FirstOrDefault(items => items is not null);
    }

    private static BackgroundItemRecord? ParseRecord(object? item)
    {
        if (item is not Dictionary<string, object?> fields ||
            fields.GetValueOrDefault("type") is not long type ||
            fields.GetValueOrDefault("disposition") is not long disposition) {

            return null;
        }

        return new BackgroundItemRecord {
            Name             = fields.GetValueOrDefault("name") as string,
            DeveloperName    = fields.GetValueOrDefault("developerName") as string,
            Identifier       = fields.GetValueOrDefault("identifier") as string,
            ParentIdentifier = fields.GetValueOrDefault("parentIdentifier") as string,
            BundleIdentifier = fields.GetValueOrDefault("bundleIdentifier") as string,
            ExecutablePath   = fields.GetValueOrDefault("executablePath") as string,
            Url              = fields.GetValueOrDefault("url") as string,
            Type             = (BackgroundItemType)type,
            Disposition      = (BackgroundItemDisposition)disposition
        };
    }

    private static string? FindNewestDatabase()
    {
        string[] files;

        try {
            files = Directory.GetFiles(DirectoryPath, "BackgroundItems-v*.btm");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            // Can still fail as root without full disk access.
            TraceEx.WriteLineOnce($"{nameof(BackgroundItemsDatabase)} {DirectoryPath}", ex.Message);
            return null;
        }

        // BackgroundItems-v<N>.btm; the highest N is the live one.
        return files
            .Select(file => (File: file, Match: DatabaseVersion().Match(Path.GetFileName(file))))
            .Where(candidate => candidate.Match.Success)
            .OrderByDescending(candidate => int.Parse(candidate.Match.Groups[1].Value))
            .Select(candidate => candidate.File)
            .FirstOrDefault();
    }

    [GeneratedRegex(@"^BackgroundItems-v(\d{1,6})\.btm$")]
    private static partial Regex DatabaseVersion();
}
#endif
