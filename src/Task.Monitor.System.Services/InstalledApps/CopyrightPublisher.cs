using System.Text.RegularExpressions;

namespace Task.Monitor.System.Services.InstalledApps;

public static partial class CopyrightPublisher
{
    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase) {
        "Co", "Corp", "Inc", "Ltd", "Pty", "Pvt"
    };

    [GeneratedRegex(@"\ball rights reserved\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex AllRightsReserved();

    private static string? Clean(string text)
    {
        string cleaned = CopyrightMarker().Replace(text, " ");
        cleaned = Years().Replace(cleaned, " ");
        cleaned = Whitespace().Replace(cleaned, " ");
        cleaned = cleaned.TrimStart(' ', ',', '.', ';', ':', '-', '–', '—');
        cleaned = LeadingBy().Replace(cleaned, string.Empty);
        cleaned = cleaned.TrimEnd(' ', ',', ';', ':', '-', '–', '—');

        if (cleaned.EndsWith('.') && !EndsWithAbbreviation(cleaned)) {
            cleaned = cleaned[..^1].TrimEnd();
        }

        return cleaned.Any(char.IsLetter)
            ? cleaned
            : null;
    }

    [GeneratedRegex(@"\bcopyright\b|©|\(c\)", RegexOptions.IgnoreCase)]
    private static partial Regex CopyrightMarker();

    private static bool EndsWithAbbreviation(string text)
    {
        string lastWord = text[(text.LastIndexOfAny([' ', ',']) + 1)..].TrimEnd('.');

        return Abbreviations.Contains(lastWord);
    }

    [GeneratedRegex(@"^by\s+", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingBy();

    public static string? Parse(string? copyright)
    {
        if (string.IsNullOrWhiteSpace(copyright)) {
            return null;
        }

        string line = copyright
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;

        line = AllRightsReserved().Replace(line, string.Empty);

        Match marker = CopyrightMarker().Match(line);

        // Without a copyright mark, only a leading year ("2024 Acme Inc.") says this is a copyright
        // notice rather than some other description.
        if (!marker.Success) {
            return Years().Match(line) is { Success: true, Index: 0 }
                ? Clean(line)
                : null;
        }

        // The name usually follows the mark ("© 2024 Acme"), occasionally precedes it ("Acme © 2024").
        return Clean(line[(marker.Index + marker.Length)..]) ?? Clean(line[..marker.Index]);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // A year or a year range: "2024", "2012-2024", "2012 – present".
    [GeneratedRegex(@"\b(?:19|20)\d{2}\b(?:\s*[-–—]\s*(?:(?:19|20)\d{2}\b|present\b|now\b))?", RegexOptions.IgnoreCase)]
    private static partial Regex Years();
}
