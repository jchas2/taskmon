using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Xml;
using System.Xml.Linq;
using Task.Monitor.Cli.Utils;

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
                string? module = Named(data, "ModuleName");

                return new HealthReport {
                    TimeUtc = timeUtc,
                    Cause   = HealthReportCause.AppError,
                    App     = Named(data, "AppName") ?? First(data),
                    Module  = module == null || module.Equals(Unknown, StringComparison.OrdinalIgnoreCase) ? string.Empty : module
                };
            }

            case ApplicationHang when eventId == 1002:
                return new HealthReport {
                    TimeUtc = timeUtc,
                    Cause   = HealthReportCause.AppHang,
                    App     = Named(data, "AppName") ?? First(data)
                };

            case ResourceDetector when eventId == 2004:
                return new HealthReport { TimeUtc = timeUtc, Cause = HealthReportCause.VirtualMemoryLow };

            case KernelPower when eventId == 41:
                return new HealthReport { TimeUtc = timeUtc, Cause = HealthReportCause.UnexpectedRestart };

            case WheaLogger:
                return new HealthReport { TimeUtc = timeUtc, Cause = HealthReportCause.HardwareError, EventId = eventId };

            default:
                return provider.Equals(DiskProvider, StringComparison.OrdinalIgnoreCase) || eventId == 129
                    ? DiskError(provider, eventId, timeUtc)
                    : null;
        }
    }

    private static HealthReport? DiskError(string provider, int eventId, DateTime timeUtc)
    {
        HealthReportCause? cause = eventId switch {
            7   => HealthReportCause.BadBlock,
            51  => HealthReportCause.PagingError,
            129 => HealthReportCause.DeviceReset,
            153 => HealthReportCause.RequestRetried,
            _   => null
        };

        return cause is { } found
            ? new HealthReport { TimeUtc = timeUtc, Cause = found, Driver = provider }
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
        catch (XmlException ex) {
            ExceptionHelper.LogException(ex);
            return null;
        }
    }
}
