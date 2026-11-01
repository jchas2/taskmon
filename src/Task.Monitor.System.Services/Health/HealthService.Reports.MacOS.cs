#if __APPLE__
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Health;

public partial class HealthService
{
    private const string ReportsFolder = "Library/Logs/DiagnosticReports";

    private static bool CanDetectHungApps => false;

    private static IReadOnlyList<DriveHealth> ReadDrives(DiskInfo? disks) => [];

    private static void ReadFolder(string folder, DateTime cutoffUtc, List<HealthReport> reports)
    {
        try {
            if (!Directory.Exists(folder)) {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(folder)) {
                DateTime timeUtc = File.GetLastWriteTimeUtc(file);

                if (timeUtc >= cutoffUtc && DiagnosticReportParser.Parse(Path.GetFileName(file), timeUtc) is { } report) {
                    reports.Add(report);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            TraceEx.WriteLineOnce(folder, $"Skipping {folder}: {ex.Message}");
        }
    }

    private static IReadOnlyCollection<int> ReadHungProcessIds() => [];

    private static IReadOnlyList<HealthReport> ReadReports(int lookbackHours, int limit)
    {
        List<HealthReport> reports = [];
        DateTime cutoffUtc = DateTime.UtcNow.AddHours(-lookbackHours);

        ReadFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ReportsFolder), cutoffUtc, reports);
        ReadFolder(Path.Combine("/", ReportsFolder), cutoffUtc, reports);

        return reports;
    }
}
#endif
