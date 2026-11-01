using System.Text;

namespace Task.Monitor.System.Configuration;

// Applies config changes to the text of an ini file and leaves everything else as it was typed, so
// comments, blank lines, ordering, line endings and hand edits all survive a save.
public static class ConfigFileMerger
{
    private const string CommentPrefix = "; ";

    private sealed class FileSection(int headerLine)
    {
        public Dictionary<string, int> Keys { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int LastLine { get; set; } = headerLine;
    }

    private static void AppendSection(StringBuilder text, ConfigSection section, string newLine)
    {
        for (int count = TrailingNewLines(text); count < 2 && text.Length > 0; count++) {
            text.Append(newLine);
        }

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

    private static string DetectNewLine(string fileText)
    {
        if (fileText.Contains("\r\n")) {
            return "\r\n";
        }

        return fileText.Contains('\n') ? "\n" : Environment.NewLine;
    }

    private static string FormatEntry(string key, string value) => $"{key}={value}";

    private static bool IsUnchanged(Config savedConfig, string sectionName, string key, string value)
    {
        if (!savedConfig.ContainsSection(sectionName)) {
            return false;
        }

        ConfigSection section = savedConfig.GetConfigSection(sectionName);
        return section.Contains(key) && section.GetString(key) == value;
    }

    // A key is only rewritten when currentConfig differs from savedConfig, the values as last
    // loaded or saved, so a key edited by hand since then keeps the edit. Missing keys and sections
    // are added. fileText must already have parsed.
    public static string Merge(string fileText, Config savedConfig, Config currentConfig)
    {
        string                          newLine       = DetectNewLine(fileText);
        List<string>                    lines         = SplitLines(fileText);
        Dictionary<string, FileSection> fileSections  = ReadSections(lines);
        Dictionary<int, List<string>>   addedKeys     = new();
        List<ConfigSection>             addedSections = new();

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

                    added.Add(FormatEntry(key, value) + newLine);
                }
                else if (!IsUnchanged(savedConfig, section.Name, key, value)) {
                    lines[line] = ReplaceValue(lines[line], value);
                }
            }
        }

        // Bottom up, so inserting doesn't move the lines still to be inserted after.
        foreach ((int line, List<string> added) in addedKeys.OrderByDescending(entry => entry.Key)) {
            if (!lines[line].EndsWith('\n')) {
                lines[line] += newLine;
            }

            lines.InsertRange(line + 1, added);
        }

        StringBuilder text = new(string.Concat(lines));

        foreach (ConfigSection section in addedSections) {
            AppendSection(text, section, newLine);
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
}
