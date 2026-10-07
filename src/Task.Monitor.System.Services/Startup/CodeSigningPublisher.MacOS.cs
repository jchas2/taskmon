#if __APPLE__
using System.Text.RegularExpressions;

namespace Task.Monitor.System.Services.Startup;

public static partial class CodeSigningPublisher
{
    private const string ApplePublisher = "Apple Inc.";
    private const string AppStorePublisher = "Mac App Store";

    public static string? Parse(string? subjectSummary)
    {
        if (string.IsNullOrWhiteSpace(subjectSummary)) {
            return null;
        }

        string summary = subjectSummary.Trim();

        if (summary == "Software Signing") {
            return ApplePublisher;
        }

        if (summary.StartsWith("Apple Mac OS Application Signing", StringComparison.Ordinal)) {
            return AppStorePublisher;
        }

        // "<certificate type>: <name> (<team id>)"
        int separator = summary.IndexOf(": ", StringComparison.Ordinal);
        string name = separator >= 0 ? summary[(separator + 2)..] : summary;
        name = TeamIdSuffix().Replace(name, string.Empty).Trim();

        return name.Length > 0 ? name : null;
    }

    [GeneratedRegex(@"\s*\([A-Z0-9]{10}\)$")]
    private static partial Regex TeamIdSuffix();
}
#endif
