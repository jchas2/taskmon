#if __WIN32__
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Health;

public partial class HealthService
{
    private const string ApplicationChannel = "Application";
    private const string GhostWindowClass   = "Ghost";
    private const string SystemChannel      = "System";

    private const string ApplicationFilter =
        "(Provider[@Name='Application Error'] and EventID=1000) or " +
        "(Provider[@Name='Application Hang'] and EventID=1002)";

    private const string SystemFilter =
        "(Provider[@Name='Microsoft-Windows-Resource-Exhaustion-Detector'] and EventID=2004) or " +
        "(Provider[@Name='Microsoft-Windows-Kernel-Power'] and EventID=41) or " +
        "(Provider[@Name='Microsoft-Windows-WHEA-Logger']) or " +
        "(Provider[@Name='disk'] and (EventID=7 or EventID=51 or EventID=153)) or " +
        "(Provider[@Name='stornvme' or @Name='storahci' or @Name='storport'] and EventID=129)";

    private const int EventBatchSize  = 32;
    private const int EventXmlChars   = 8192;
    private const int HealthLogSize   = 512;
    private const int MillisPerHour   = 3_600_000;
    private const int MaxXmlChars     = 1 << 20;

    private static bool CanDetectHungApps => true;

    internal static bool IsReadOnlyVolume(string name) => false;

    private static IReadOnlyList<DriveHealth> ReadDrives(DiskInfo? disks)
    {
        List<DriveHealth> drives = [];

        foreach (DiskDevice disk in disks?.Specs.Devices ?? []) {
            if (!string.Equals(disk.BusType, "NVMe", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            byte[] log = new byte[HealthLogSize];

            if (Nvme.TryQueryLogPage(disk.Index, WinIoCtl.NVMeLogPageHealthInfo, log) &&
                NvmeHealthLog.CriticalWarning(log) is { } warning &&
                NvmeHealthLog.PercentUsed(log) is { } percentUsed &&
                NvmeHealthLog.MediaErrors(log) is { } mediaErrors) {

                drives.Add(new DriveHealth($"Disk {disk.Index}", warning, percentUsed, mediaErrors));
            }
        }

        return drives;
    }

    private static unsafe void ReadEvents(string channel, string filter, int lookbackHours, int limit, List<HealthReport> reports)
    {
        long   lookbackMillis = (long)lookbackHours * MillisPerHour;
        string query          = $"*[System[({filter}) and TimeCreated[timediff(@SystemTime) <= {lookbackMillis}]]]";

        nint results = WinEvt.EvtQuery(
            nint.Zero,
            channel,
            query,
            WinEvt.EvtQueryChannelPath | WinEvt.EvtQueryReverseDirection);

        if (results == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(WinEvt.EvtQuery), $"Failed reading the {channel} event log");
            return;
        }

        try {
            nint* records = stackalloc nint[EventBatchSize];
            int   read    = 0;

            while (read < limit) {
                uint returned = 0;

                if (!WinEvt.EvtNext(results, EventBatchSize, records, WinEvt.Infinite, 0, &returned)) {
                    break;
                }

                for (uint i = 0; i < returned; i++) {
                    if (read < limit && ReadEventXml(records[i]) is { } xml && EventLogReportParser.Parse(xml) is { } report) {
                        reports.Add(report);
                        read++;
                    }

                    WinEvt.EvtClose(records[i]);
                }
            }
        }
        finally {
            WinEvt.EvtClose(results);
        }
    }

    private static unsafe string? ReadEventXml(nint record)
    {
        char[] buffer = new char[EventXmlChars];

        while (buffer.Length <= MaxXmlChars) {
            uint used       = 0;
            uint properties = 0;

            fixed (char* text = buffer) {
                bool rendered = WinEvt.EvtRender(
                    nint.Zero,
                    record,
                    WinEvt.EvtRenderEventXml,
                    (uint)buffer.Length * sizeof(char),
                    text,
                    &used,
                    &properties);

                if (rendered) {
                    return new string(text, 0, Math.Max(0, (int)used / sizeof(char) - 1));
                }
            }

            if (Marshal.GetLastWin32Error() != WinEvt.ERROR_INSUFFICIENT_BUFFER) {
                return null;
            }

            buffer = new char[used / sizeof(char) + 1];
        }

        return null;
    }

    private static IReadOnlyCollection<int> ReadHungProcessIds()
    {
        HashSet<int> pids = [];

        foreach (nint window in WinUser.GetTopLevelWindows()) {
            if (WinUser.IsWindowVisible(window) && 
                WinUser.IsHungAppWindow(window) && 
                WinUser.GetWindowClassName(window) != GhostWindowClass) {
                
                WinUser.GetWindowThreadProcessId(window, out uint pid);
                pids.Add((int)pid);
            }
        }

        return pids;
    }

    private static IReadOnlyList<HealthReport> ReadReports(int lookbackHours, int limit)
    {
        List<HealthReport> reports = [];

        ReadEvents(
            ApplicationChannel, 
            ApplicationFilter, 
            lookbackHours, 
            limit, 
            reports);
        
        ReadEvents(
            SystemChannel, 
            SystemFilter, 
            lookbackHours, 
            limit, 
            reports);

        return reports;
    }
}
#endif
