using System.Text;

namespace Task.Monitor.System.Configuration;

public static class ConfigFileMerger
{
    private const string CommentPrefix = "; ";

    private sealed class FileSection(int headerLine)
    {
        public int HeaderLine { get; } = headerLine;

        public Dictionary<string, int> Keys { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int LastLine { get; set; } = headerLine;
    }

    private sealed class Template(List<string> lines)
    {
        public List<string> Lines { get; } = lines;

        public Dictionary<string, FileSection> Sections { get; } = ReadSections(lines);
    }

    private static List<string> AddedKeyLines(Template? template, string sectionName, string key, string value, string newLine)
    {
        List<string> added = new();

        if (template != null &&
            template.Sections.TryGetValue(sectionName, out FileSection? templateSection) &&
            templateSection.Keys.TryGetValue(key, out int keyLine)) {

            int commentStart = CommentStart(template.Lines, keyLine);

            if (commentStart < keyLine) {
                added.Add(newLine);

                for (int i = commentStart; i < keyLine; i++) {
                    added.Add(WithoutNewLine(template.Lines[i]) + newLine);
                }
            }
        }

        added.Add(FormatEntry(key, value) + newLine);
        return added;
    }

    private static void AppendBlankLines(StringBuilder text, string newLine)
    {
        for (int count = TrailingNewLines(text); count < 2 && text.Length > 0; count++) {
            text.Append(newLine);
        }
    }

    private static void AppendSection(StringBuilder text, ConfigSection section, string newLine)
    {
        AppendBlankLines(text, newLine);

        if (section.Comment.Length > 0) {
            foreach (string commentLine in section.Comment.Split('\n')) {
                text.Append(CommentPrefix).Append(commentLine).Append(newLine);
            }
        }

        text.Append($"[{section.Name}]").Append(newLine);

        foreach ((string key, string value) in section.Entries) {
            text.Append(FormatEntry(key, value)).Append(newLine);
        }
    }

    private static void AppendTemplateSection(
        StringBuilder text,
        ConfigSection section,
        Template      template,
        FileSection   templateSection,
        string        newLine)
    {
        AppendBlankLines(text, newLine);

        int start = CommentStart(template.Lines, templateSection.HeaderLine);
        int end   = template.Sections.Values
            .Where(other => other.HeaderLine > templateSection.HeaderLine)
            .Select(other => CommentStart(template.Lines, other.HeaderLine))
            .DefaultIfEmpty(template.Lines.Count)
            .Min();

        while (end > start && template.Lines[end - 1].Trim().Length == 0) {
            end--;
        }

        Dictionary<int, string> keyLines = templateSection.Keys.ToDictionary(entry => entry.Value, entry => entry.Key);

        for (int i = start; i < end; i++) {
            string line = WithoutNewLine(template.Lines[i]);

            if (keyLines.TryGetValue(i, out string? key) && section.Contains(key)) {
                line = ReplaceValue(line, section.GetString(key));
            }

            text.Append(line).Append(newLine);
        }

        foreach ((string key, string value) in section.Entries) {
            if (!templateSection.Keys.ContainsKey(key)) {
                text.Append(FormatEntry(key, value)).Append(newLine);
            }
        }
    }

    private static int CommentStart(List<string> lines, int line)
    {
        while (line > 0 && IsComment(lines[line - 1])) {
            line--;
        }

        return line;
    }

    private static string DetectNewLine(string fileText)
    {
        if (fileText.Contains("\r\n")) {
            return "\r\n";
        }

        return fileText.Contains('\n') ? "\n" : Environment.NewLine;
    }

    private static string FormatEntry(string key, string value) => $"{key}={value}";

    private static bool IsComment(string line) => line.TrimStart() is [';' or '#', ..];

    private static bool IsUnchanged(Config savedConfig, string sectionName, string key, string value)
    {
        if (!savedConfig.ContainsSection(sectionName)) {
            return false;
        }

        ConfigSection section = savedConfig.GetConfigSection(sectionName);
        return section.Contains(key) && section.GetString(key) == value;
    }

    public static string Merge(string fileText, Config savedConfig, Config currentConfig, string? templateText = null)
    {
        string                          newLine       = DetectNewLine(fileText);
        List<string>                    lines         = SplitLines(fileText);
        Dictionary<string, FileSection> fileSections  = ReadSections(lines);
        Dictionary<int, List<string>>   addedKeys     = new();
        List<ConfigSection>             addedSections = new();
        Template?                       template      = string.IsNullOrEmpty(templateText) ? null : new(SplitLines(templateText));

        foreach (ConfigSection section in currentConfig.ConfigSections) {
            if (!fileSections.TryGetValue(section.Name, out FileSection? fileSection)) {
                addedSections.Add(section);
                continue;
            }

            foreach ((string key, string value) in section.Entries) {
                if (!fileSection.Keys.TryGetValue(key, out int line)) {
                    if (!addedKeys.TryGetValue(fileSection.LastLine, out List<string>? added)) {
                        added = addedKeys[fileSection.LastLine] = new();
                    }

                    added.AddRange(AddedKeyLines(template, section.Name, key, value, newLine));
                }
                else if (!IsUnchanged(savedConfig, section.Name, key, value)) {
                    lines[line] = ReplaceValue(lines[line], value);
                }
            }
        }

        foreach ((int line, List<string> added) in addedKeys.OrderByDescending(entry => entry.Key)) {
            if (!lines[line].EndsWith('\n')) {
                lines[line] += newLine;
            }

            lines.InsertRange(line + 1, added);
        }

        StringBuilder text = new(string.Concat(lines));

        foreach (ConfigSection section in addedSections) {
            if (template != null && template.Sections.TryGetValue(section.Name, out FileSection? templateSection)) {
                AppendTemplateSection(text, section, template, templateSection, newLine);
            }
            else {
                AppendSection(text, section, newLine);
            }
        }

        return text.ToString();
    }

    private static Dictionary<string, FileSection> ReadSections(List<string> lines)
    {
        Dictionary<string, FileSection> sections = new(StringComparer.OrdinalIgnoreCase);
        FileSection?                    current  = null;

        for (int i = 0; i < lines.Count; i++) {
            string text = lines[i].Trim();

            if (text.StartsWith('[')) {
                current = new FileSection(i);
                sections.TryAdd(text[1..text.IndexOf(']')], current);
                continue;
            }

            int equals = text.IndexOf('=');

            if (current == null || equals < 0 || text[0] is ';' or '#') {
                continue;
            }

            // The parser keeps the first of a repeated key, so that is the one to update.
            current.Keys.TryAdd(text[..equals].TrimEnd(), i);
            current.LastLine = i;
        }

        return sections;
    }

    // Replaces only the value, keeping the key, the spacing around the value, any inline comment
    // and the line ending.
    private static string ReplaceValue(string line, string value)
    {
        int    afterEquals  = line.IndexOf('=') + 1;
        int    lineEnd      = line.TrimEnd('\r', '\n').Length;
        int    commentStart = line.IndexOf(';', afterEquals, lineEnd - afterEquals);
        string oldValue     = line[afterEquals..(commentStart >= 0 ? commentStart : lineEnd)];
        string fromValue    = oldValue.TrimStart();
        int    start        = afterEquals + oldValue.Length - fromValue.Length;
        int    end          = start + fromValue.TrimEnd().Length;

        return string.Concat(line.AsSpan(0, start), value, line.AsSpan(end));
    }

    private static List<string> SplitLines(string fileText)
    {
        List<string> lines = new();
        int          start = 0;

        while (start < fileText.Length) {
            int end = fileText.IndexOf('\n', start);
            int next = end < 0 ? fileText.Length : end + 1;

            lines.Add(fileText[start..next]);
            start = next;
        }

        return lines;
    }

    private static int TrailingNewLines(StringBuilder text)
    {
        int count = 0;

        for (int i = text.Length - 1; i >= 0 && char.IsWhiteSpace(text[i]); i--) {
            if (text[i] == '\n') {
                count++;
            }
        }

        return count;
    }

    private static string WithoutNewLine(string line) => line.TrimEnd('\r', '\n');
}
