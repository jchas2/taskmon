using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Task.Monitor.System.Services.Health;

internal static class EventLogReportParser
{
    private const string ApplicationError = "Application Error";
    private const string ApplicationHang  = "Application Hang";
    private const string DiskProvider     = "disk";
    private const string KernelPower      = "Microsoft-Windows-Kernel-Power";
    private const string ResourceDetector = "Microsoft-Windows-Resource-Exhaustion-Detector";
    private const string WheaLogger       = "Microsoft-Windows-WHEA-Logger";

    private const string Unknown = "unknown";

    private static HealthReport? Build(string provider, int eventId, DateTime timeUtc, IReadOnlyList<XElement> data)
    {
        switch (provider) {
            case ApplicationError when eventId == 1000: {
                string  app    = Named(data, "AppName") ?? First(data);
                string? module = Named(data, "ModuleName");

                // Windows records "unknown" itself for some crashes, which says nothing to show.
                string detail = module == null || module.Equals(Unknown, StringComparison.OrdinalIgnoreCase)
                    ? app
                    : $"{app}, in {module}";

                return new HealthReport(timeUtc, HealthReportKind.AppCrash, "App crash", detail, app);
            }

            case ApplicationHang when eventId == 1002: {
                string app = Named(data, "AppName") ?? First(data);

                return new HealthReport(timeUtc, HealthReportKind.AppHang, "App hang", app, app);
            }

            case ResourceDetector when eventId == 2004:
                return new HealthReport(timeUtc, HealthReportKind.LowMemory, "Low memory", "Windows ran low on virtual memory", string.Empty);

            case KernelPower when eventId == 41:
                return new HealthReport(timeUtc, HealthReportKind.UnexpectedRestart, "Unexpected restart", "Restarted without shutting down cleanly", string.Empty);

            case WheaLogger:
                return new HealthReport(timeUtc, HealthReportKind.HardwareError, "Hardware error", $"WHEA event {eventId}", string.Empty);

            default:
                return provider.Equals(DiskProvider, StringComparison.OrdinalIgnoreCase) || eventId == 129
                    ? DiskError(provider, eventId, timeUtc)
                    : null;
        }
    }

    private static HealthReport? DiskError(string provider, int eventId, DateTime timeUtc)
    {
        string? detail = eventId switch {
            7   => "Bad block",
            51  => "Error during a paging operation",
            129 => $"{provider} reset the device",
            153 => "Request retried",
            _   => null
        };

        return detail != null
            ? new HealthReport(timeUtc, HealthReportKind.DiskError, "Disk error", detail, string.Empty)
            : null;
    }

    private static string First(IReadOnlyList<XElement> data) =>
        data.Count > 0 && data[0].Value.Length > 0 ? data[0].Value : Unknown;

    private static string? Named(IReadOnlyList<XElement> data, string name) =>
        data.FirstOrDefault(element => element.Attribute("Name")?.Value == name)?.Value;

    public static HealthReport? Parse(string xml)
    {
        try {
            XElement  root   = XElement.Parse(xml);
            XNamespace space = root.Name.Namespace;
            XElement? system = root.Element(space + "System");

            string? provider = system?.Element(space + "Provider")?.Attribute("Name")?.Value;
            string? eventId  = system?.Element(space + "EventID")?.Value;
            string? time     = system?.Element(space + "TimeCreated")?.Attribute("SystemTime")?.Value;

            if (provider == null ||
                !int.TryParse(eventId, CultureInfo.InvariantCulture, out int id) ||
                !DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime timeUtc)) {

                return null;
            }

            List<XElement> data = root.Element(space + "EventData")?.Elements(space + "Data").ToList() ?? [];

            return Build(provider, id, timeUtc, data);
        }
        catch (XmlException) {
            return null;
        }
    }
}
