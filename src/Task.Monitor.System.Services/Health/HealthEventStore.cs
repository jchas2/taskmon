using System.Diagnostics;
using System.Globalization;
using System.Text;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Configuration;

namespace Task.Monitor.System.Services.Health;

public sealed class HealthEventStore(IFileSystem fileSystem, string folder)
{
    private const string CulpritKeyPrefix = "culprit-";
    private const int    CurrentVersion   = 1;
    private const string EndKey           = "end";
    private const char   EscapeChar       = '\\';
    private const string FileNamePattern  = "????????T??????Z-*.ini";
    private const string FileTimeFormat   = "yyyyMMdd'T'HHmmss'Z'";
    private const string LevelKey         = "level";
    private const string PeakReasonKey    = "peak-reason";
    private const string PeakValueKey     = "peak-value";
    private const string ResourceKey      = "resource";
    private const string RoundTripNumber  = "R";
    private const string RoundTripTime    = "O";
    private const string SectionName      = "event";
    private const string StartKey         = "start";
    private const string VersionKey       = "version";

    internal static string Escape(string text)
    {
        StringBuilder escaped = new(text.Length);

        foreach (char ch in text) {
            switch (ch) {
                case EscapeChar: escaped.Append(EscapeChar).Append(EscapeChar); break;
                case ';':        escaped.Append(EscapeChar).Append('s');        break;
                case '\r':       escaped.Append(EscapeChar).Append('r');        break;
                case '\n':       escaped.Append(EscapeChar).Append('n');        break;
                default:         escaped.Append(ch);                            break;
            }
        }

        return escaped.ToString();
    }

    public IReadOnlyList<HealthEvent> Load(int limit)
    {
        List<HealthEvent> events = [];

        foreach (string file in SortedFiles().TakeLast(limit)) {
            Trace.WriteLine($"Loading event file {file}");
            if (Read(file) is { } healthEvent) {
                events.Add(healthEvent);
            }
        }

        return events;
    }

    private static HealthEvent? Parse(ConfigSection section)
    {
        if (section.GetInt(VersionKey) != CurrentVersion ||
            !Enum.TryParse(section.GetString(ResourceKey), out HealthResource resource) ||
            !Enum.TryParse(section.GetString(LevelKey), out HealthLevel level) ||
            !TryParseTime(section.GetString(StartKey), out DateTime startUtc)) {

            return null;
        }

        DateTime? endUtc = TryParseTime(section.GetString(EndKey), out DateTime end) ? end : null;

        _ = double.TryParse(
            section.GetString(PeakValueKey),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double peakValue);

        return new HealthEvent {
            Resource      = resource,
            Level         = level,
            StartUtc      = startUtc,
            EndUtc        = endUtc,
            IsInterrupted = endUtc == null,
            PeakReason    = Unescape(section.GetString(PeakReasonKey)),
            PeakValue     = peakValue,
            Culprits      = ReadCulprits(section)
        };
    }

    private string PathFor(HealthEvent healthEvent)
    {
        string time     = healthEvent.StartUtc.ToString(FileTimeFormat, CultureInfo.InvariantCulture);
        string resource = healthEvent.Resource.ToString().ToLowerInvariant();

        return Path.Combine(folder, $"{time}-{resource}-{Environment.ProcessId}.ini");
    }

    public void Prune(int limit)
    {
        string[] files = SortedFiles();

        foreach (string file in files.Take(files.Length - limit)) {
            try {
                fileSystem.DeleteFile(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                TraceEx.WriteLineOnce(file, $"Skipping pruning {file}: {ex.Message}");
            }
        }
    }

    private HealthEvent? Read(string file)
    {
        try {
            return Parse(Config.FromFile(fileSystem, file).GetConfigSection(SectionName));
        }
        catch (Exception ex) when (ex is ConfigParseException or
                                         InvalidOperationException or
                                         IOException or
                                         UnauthorizedAccessException) {

            TraceEx.WriteLineOnce(file, $"Skipping health event {file}: {ex.Message}");
            return null;
        }
    }

    private static List<string> ReadCulprits(ConfigSection section)
    {
        List<string> culprits = [];

        for (int i = 0; section.Contains($"{CulpritKeyPrefix}{i}"); i++) {
            culprits.Add(Unescape(section.GetString($"{CulpritKeyPrefix}{i}")));
        }

        return culprits;
    }

    public void Save(HealthEvent healthEvent)
    {
        string path = PathFor(healthEvent);

        fileSystem.WriteAllTextAtomic(path, Serialise(healthEvent));
        PathPermissions.EnsureUserOwnership(path);
    }

    internal static string Serialise(HealthEvent healthEvent)
    {
        ConfigSection section = new ConfigSection(SectionName)
            .Add(VersionKey, CurrentVersion.ToString(CultureInfo.InvariantCulture))
            .Add(ResourceKey, healthEvent.Resource.ToString())
            .Add(LevelKey, healthEvent.Level.ToString())
            .Add(StartKey, healthEvent.StartUtc.ToString(RoundTripTime, CultureInfo.InvariantCulture));

        if (healthEvent.EndUtc is { } endUtc) {
            section.Add(EndKey, endUtc.ToString(RoundTripTime, CultureInfo.InvariantCulture));
        }

        section
            .Add(PeakReasonKey, Escape(healthEvent.PeakReason))
            .Add(PeakValueKey, healthEvent.PeakValue.ToString(RoundTripNumber, CultureInfo.InvariantCulture));

        for (int i = 0; i < healthEvent.Culprits.Count; i++) {
            section.Add($"{CulpritKeyPrefix}{i}", Escape(healthEvent.Culprits[i]));
        }

        return section.ToString();
    }

    private string[] SortedFiles()
    {
        string[] files = fileSystem.GetFiles(folder, FileNamePattern);

        Array.Sort(files, StringComparer.Ordinal);
        return files;
    }

    private static bool TryParseTime(string text, out DateTime timeUtc) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out timeUtc);

    internal static string Unescape(string text)
    {
        StringBuilder unescaped = new(text.Length);

        for (int i = 0; i < text.Length; i++) {
            if (text[i] != EscapeChar || i == text.Length - 1) {
                unescaped.Append(text[i]);
                continue;
            }

            i++;

            unescaped.Append(text[i] switch {
                's' => ';',
                'r' => '\r',
                'n' => '\n',
                  _ => text[i]
            });
        }

        return unescaped.ToString();
    }
}
