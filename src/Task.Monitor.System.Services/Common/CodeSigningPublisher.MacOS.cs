#if __APPLE__
using System.Text.RegularExpressions;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Common;

public static partial class CodeSigningPublisher
{
    private const string ApplePublisher    = "Apple Inc.";
    private const string AppStorePublisher = "Mac App Store";
    private const string UserLocalPrefix   = "/usr/local/";

    private static readonly string[] SealedSystemPrefixes = ["/System/", "/usr/", "/bin/", "/sbin/"];

    private static bool IsOnSealedSystemVolume(string path) =>
        SealedSystemPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal)) &&
        !path.StartsWith(UserLocalPrefix, StringComparison.Ordinal);

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
        string name   = separator >= 0 ? summary[(separator + 2)..] : summary;
        
        name = TeamIdSuffix().Replace(name, string.Empty).Trim();

        return name.Length > 0 
            ? name 
            : null;
    }

    public static string? Resolve(string path)
    {
        if (Parse(CodeSigning.GetSigningCertificateSummary(path)) is { } publisher) {
            return publisher;
        }

        return IsOnSealedSystemVolume(path)
            ? ApplePublisher
            : null;
    }

    [GeneratedRegex(@"\s*\([A-Z0-9]{10}\)$")]
    private static partial Regex TeamIdSuffix();
}
#endif
