using System.Drawing;
using System.Globalization;
using System.Reflection;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.Gui.Controls.Mascot;

public sealed class MascotAnimation
{
    internal const char TransparentPixel = '.';

    private const string ResourceSuffix = ".Assets.Mascot.Mascot.anim";

    private readonly Dictionary<string, MascotFrame>               frames;
    private readonly Dictionary<string, IReadOnlyList<MascotStep>> timelines;

    private sealed class Section(int line)
    {
        public int                                Line  { get; } = line;
        public List<(string Text, int Line)>      Lines { get; } = [];
    }

    private MascotAnimation(
        Dictionary<string, MascotFrame>               frames,
        Dictionary<string, IReadOnlyList<MascotStep>> timelines,
        MascotIdle?                                   idle)
    {
        this.frames    = frames;
        this.timelines = timelines;
        Idle           = idle;

        MascotFrame first = frames.Values.First();

        Width  = first.Width;
        Height = first.Height;
    }

    private static Dictionary<string, MascotFrame> BuildFrames(
        Dictionary<string, Section> frameSections,
        Dictionary<char, Color>     palette,
        int                         lastLine)
    {
        if (frameSections.Count == 0) {
            throw Error(lastLine, "no frames");
        }

        Section sizing = frameSections.Values.First();
        int     width  = sizing.Lines.Count > 0 ? sizing.Lines[0].Text.Length : 0;
        int     height = sizing.Lines.Count;

        Dictionary<string, MascotFrame> frames = new(StringComparer.Ordinal);

        foreach ((string name, Section section) in frameSections) {
            if (height == 0 || height % 2 != 0 || section.Lines.Count != height) {
                throw Error(section.Line,
                    $"frame '{name}' has {section.Lines.Count} pixel rows; every frame needs the same, even, non-zero number");
            }

            foreach ((string row, int line) in section.Lines) {
                if (row.Length != width) {
                    throw Error(line, $"frame '{name}' row is {row.Length} pixels wide, not {width}");
                }

                foreach (char pixel in row) {
                    if (pixel != TransparentPixel && !palette.ContainsKey(pixel)) {
                        throw Error(line, $"'{pixel}' is not in the palette");
                    }
                }
            }

            frames[name] = MascotFrame.FromPixels(
                section.Lines.Select(entry => entry.Text).ToList(), palette);
        }

        return frames;
    }

    private static List<MascotStep> BuildTimeline(string name, Section section, Dictionary<string, MascotFrame> frames)
    {
        if (section.Lines.Count == 0) {
            throw Error(section.Line, $"timeline '{name}' has no steps");
        }

        List<MascotStep> steps = [];

        for (int i = 0; i < section.Lines.Count; i++) {
            (string text, int line) = section.Lines[i];

            string[] fields = Fields(text, 3, line, "frame, ms and x");
            
            MascotStep step = new(
                fields[0], 
                Number(
                    fields[1], 
                    line, 
                    minimum: 0), 
                Number(
                    fields[2], 
                    line, 
                    minimum: int.MinValue));

            if (!frames.ContainsKey(step.Frame)) {
                throw Error(line, $"unknown frame '{step.Frame}'");
            }

            if (step.Milliseconds == 0 && i != section.Lines.Count - 1) {
                throw Error(line, "only the last step can be 0 ms");
            }

            steps.Add(step);
        }

        return steps;
    }

    private static FormatException Error(int line, string message) =>
        new($"Mascot.anim line {line}: {message}");

    private static string[] Fields(
        string text, 
        int    count, 
        int    line, 
        string expected)
    {
        string[] fields = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return fields.Length == count
            ? fields
            : throw Error(line, $"expected {expected}");
    }

    public IReadOnlyCollection<string> FrameNames => frames.Keys;

    public MascotFrame GetFrame(string name) => frames[name];

    public IReadOnlyList<MascotStep> GetTimeline(string name) => timelines[name];

    public int Height { get; }

    public MascotIdle? Idle { get; }

    public static MascotAnimation LoadEmbedded()
    {
        Assembly asm  = typeof(MascotAnimation).Assembly;
        string   name = asm.GetManifestResourceNames().Single(resource => resource.EndsWith(ResourceSuffix));

        using StreamReader reader = new(asm.GetManifestResourceStream(name)!);

        return Parse(reader.ReadToEnd());
    }

    private static int Number(string field, int line, int minimum) =>
        int.TryParse(field, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) && value >= minimum
            ? value
            : throw Error(line, $"'{field}' is not a valid number here");

    public static MascotAnimation Parse(string text)
    {
        Dictionary<char, Color>     palette  = [];
        Dictionary<string, Section> frames   = new(StringComparer.Ordinal);
        Dictionary<string, Section> timeline = new(StringComparer.Ordinal);
        Section?                    idle     = null;
        Section?                    current  = null;
        string                      kind     = string.Empty;
        int                         line     = 0;

        foreach (string raw in text.Split('\n')) {
            line++;

            int    comment = raw.IndexOf(';');
            string content = (comment >= 0 ? raw[..comment] : raw).Trim();

            if (content.Length == 0) {
                continue;
            }

            if (content[0] == '[') {
                (kind, string? name) = ParseHeader(content, line);

                current = kind switch {
                    "frame"    => frames.TryAdd(name!, new Section(line))
                        ? frames[name!]
                        : throw Error(line, $"frame '{name}' is defined twice"),
                    "timeline" => timeline.TryAdd(name!, new Section(line))
                        ? timeline[name!]
                        : throw Error(line, $"timeline '{name}' is defined twice"),
                    "idle"     => idle is null
                        ? idle = new Section(line)
                        : throw Error(line, "[idle] is defined twice"),
                             _ => null
                };

                continue;
            }

            if (kind == "palette") {
                ParsePaletteEntry(content, line, palette);
            }
            else if (current is not null) {
                current.Lines.Add((content, line));
            }
            else {
                throw Error(line, "text outside a section");
            }
        }

        Dictionary<string, MascotFrame> built = BuildFrames(frames, palette, line);

        return new MascotAnimation(
            built,
            timeline.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<MascotStep>)BuildTimeline(pair.Key, pair.Value, built),
                StringComparer.Ordinal),
            idle is null ? null : ParseIdle(idle, built));
    }

    private static (string Kind, string? Name) ParseHeader(string content, int line)
    {
        if (content[^1] != ']') {
            throw Error(line, "section header is missing its ']'");
        }

        string[] parts = content[1..^1].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string   kind  = parts.Length > 0 ? parts[0] : string.Empty;
        string?  name  = parts.Length > 1 ? parts[1] : null;
        bool     named = kind is "frame" or "timeline";

        if (kind is not ("palette" or "frame" or "timeline" or "idle")) {
            throw Error(line, $"unknown section '[{kind}]'");
        }

        if (named != (name is not null)) {
            throw Error(line, named ? $"[{kind}] needs a name" : $"[{kind}] doesn't take a name");
        }

        return (kind, name);
    }

    private static MascotIdle ParseIdle(Section section, Dictionary<string, MascotFrame> frames)
    {
        if (section.Lines.Count != 1) {
            throw Error(section.Line, "[idle] takes a single line");
        }

        (string text, int line) = section.Lines[0];

        string[]   fields = Fields(text, 4, line, "frame, ms, every and extra");
        MascotIdle idle   = new(
            fields[0],
            Number(fields[1], line, minimum: 1),
            Number(fields[2], line, minimum: 0),
            Number(fields[3], line, minimum: 0));

        return frames.ContainsKey(idle.Frame)
            ? idle
            : throw Error(line, $"unknown frame '{idle.Frame}'");
    }

    private static void ParsePaletteEntry(string content, int line, Dictionary<char, Color> palette)
    {
        string[] pair = content.Split('=', 2, StringSplitOptions.TrimEntries);

        if (pair.Length != 2 || pair[0].Length != 1 || pair[0][0] == TransparentPixel) {
            throw Error(line, "palette entries look like 'c=#RRGGBB', one letter other than '.'");
        }

        Color colour = ConsolePalette.FromHex(pair[1], ConsolePalette.Transparent);

        if (colour.IsTransparent()) {
            throw Error(line, $"'{pair[1]}' is not a #RRGGBB colour");
        }

        if (!palette.TryAdd(pair[0][0], colour)) {
            throw Error(line, $"'{pair[0]}' is in the palette twice");
        }
    }

    public int Width { get; }
}
