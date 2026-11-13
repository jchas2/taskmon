using System.Text.RegularExpressions;

namespace Task.Monitor.System.Services.Health;

internal static partial class DiagnosticReportParser
{
    private const string CpuEvent          = "cpu usage";
    private const string DiskWritesEvent   = "disk writes";
    private const string JetsamEventPrefix = "JetsamEvent";
    private const string SimulatedMarker   = "\"is_simulated\":1";

    [GeneratedRegex(@"^(?<app>.+?)[-_]\d{4}-\d{2}-\d{2}-\d{6}")]
    private static partial Regex AppName();

    public static bool IsSimulated(string? header) =>
        header?.Contains(SimulatedMarker, StringComparison.Ordinal) == true;

    public static HealthReport? Parse(string fileName, DateTime timeUtc, string? diagEvent = null)
    {
        if (fileName.StartsWith(JetsamEventPrefix, StringComparison.OrdinalIgnoreCase)) {
            return new HealthReport { TimeUtc = timeUtc, Cause = HealthReportCause.JetsamReport };
        }

        HealthReportCause? cause = fileName switch {
            _ when fileName.EndsWith(".cpu_resource.diag", StringComparison.OrdinalIgnoreCase) || diagEvent == CpuEvent
                => HealthReportCause.CpuLimitReport,
            _ when fileName.EndsWith(".diskwrites_resource.diag", StringComparison.OrdinalIgnoreCase) || diagEvent == DiskWritesEvent
                => HealthReportCause.DiskWriteLimitReport,
            _ when fileName.EndsWith(".hang", StringComparison.OrdinalIgnoreCase)
                => HealthReportCause.HangReport,
            _ when fileName.EndsWith(".spin", StringComparison.OrdinalIgnoreCase)
                => HealthReportCause.SpinReport,
            _ when fileName.EndsWith(".ips", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".crash", StringComparison.OrdinalIgnoreCase)
                => HealthReportCause.CrashReport,
            _ => null
        };

        if (cause is not { } found) {
            return null;
        }

        Match match = AppName().Match(fileName);

        string app  = match.Success
            ? match.Groups["app"].Value
            : Path.GetFileNameWithoutExtension(fileName);

        return new HealthReport { TimeUtc = timeUtc, Cause = found, App = app };
    }
}
