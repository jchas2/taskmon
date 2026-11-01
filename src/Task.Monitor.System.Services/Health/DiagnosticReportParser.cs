using System.Text.RegularExpressions;

namespace Task.Monitor.System.Services.Health;

internal static partial class DiagnosticReportParser
{
    [GeneratedRegex(@"^(?<app>.+?)[-_]\d{4}-\d{2}-\d{2}-\d{6}")]
    private static partial Regex AppName();

    public static HealthReport? Parse(string fileName, DateTime timeUtc)
    {
        (HealthReportKind Kind, string Title, string Detail)? report = fileName switch {
            _ when fileName.EndsWith(".cpu_resource.diag", StringComparison.OrdinalIgnoreCase)
                => (HealthReportKind.ResourceLimit, "CPU limit", "Used too much CPU"),
            _ when fileName.EndsWith(".diskwrites_resource.diag", StringComparison.OrdinalIgnoreCase)
                => (HealthReportKind.ResourceLimit, "Disk write limit", "Wrote too much to disk"),
            _ when fileName.EndsWith(".hang", StringComparison.OrdinalIgnoreCase)
                => (HealthReportKind.AppHang, "App hang", "Stopped responding"),
            _ when fileName.EndsWith(".spin", StringComparison.OrdinalIgnoreCase)
                => (HealthReportKind.AppHang, "App hang", "Spun without responding"),
            _ when fileName.EndsWith(".ips", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".crash", StringComparison.OrdinalIgnoreCase)
                => (HealthReportKind.AppCrash, "App crash", "Crashed"),
            _ => null
        };

        if (report is not { } found) {
            return null;
        }

        Match match = AppName().Match(fileName);
        
        string app  = match.Success 
            ? match.Groups["app"].Value 
            : Path.GetFileNameWithoutExtension(fileName);

        return new HealthReport(timeUtc, found.Kind, found.Title, $"{app}: {found.Detail}", app);
    }
}
