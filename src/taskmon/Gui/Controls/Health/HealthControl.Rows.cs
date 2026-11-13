using System.Drawing;
using System.Globalization;
using Task.Monitor.Extensions;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Health;

namespace Task.Monitor.Gui.Controls.Health;

public sealed partial class HealthControl
{
    private const int    DaysPerWeek = 7;
    private const string EmptyCell   = "—";
    private const char   StatusGlyph = '●';

    public static readonly (string Key, string Label) ShowProcessHotKey = ("Enter", "Show Process");

    private static string Cell(string text) => string.IsNullOrEmpty(text) ? EmptyCell : text;

    private static string DescribeDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1.0) {
            return $"{Math.Max(0, (int)duration.TotalSeconds)} s";
        }

        return duration.TotalHours < 1.0
            ? $"{(int)duration.TotalMinutes} min"
            : $"{(int)duration.TotalHours} h {duration.Minutes} min";
    }

    private static string DescribeEvent(HealthEvent healthEvent)
    {
        string duration = healthEvent.EndUtc is { } end ? DescribeDuration(end - healthEvent.StartUtc)
                        : healthEvent.IsInterrupted     ? "interrupted"
                                                        : "ongoing";

        return healthEvent.PeakCause is { } cause
            ? $"{duration}, peak {cause.ToReasonText()}"
            : duration;
    }

    internal static string DescribeHeadline(HealthInfo info)
    {
        if (info.Level == HealthLevel.Unknown) {
            return "Gathering metrics";
        }

        if (info.Level == HealthLevel.Ok) {
            return "Healthy";
        }

        IEnumerable<string> pressured = info.Resources
            .Where(reading => reading.Level >= HealthLevel.Warning)
            .Select(reading => ResourceName(reading.Resource));

        string headline = $"Under pressure: {string.Join(", ", pressured)}";

        return info.SinceUtc is { } since
            ? $"{headline}, since {since.ToLocalTime():HH:mm:ss}"
            : headline;
    }

    internal static string DescribeTime(DateTime timeUtc, DateTime nowUtc)
    {
        DateTime time  = timeUtc.ToLocalTime();
        DateTime today = nowUtc.ToLocalTime().Date;

        string format = time.Date == today                         ? "HH:mm:ss"
                      : (today - time.Date).TotalDays < DaysPerWeek ? "ddd HH:mm"
                                                                    : "dd MMM HH:mm";

        return time.ToString(format, CultureInfo.CurrentCulture);
    }

    private ListViewItem EventRow(HealthEvent healthEvent)
    {
        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        return new ListViewItem(
            [new ListViewSubItem(null!, DescribeTime(healthEvent.StartUtc, DateTime.UtcNow), background, foreground),
             new ListViewSubItem(null!, $"{ResourceName(healthEvent.Resource)} → {healthEvent.Level}", background, StatusColour(healthEvent.Level)),
             new ListViewSubItem(null!, DescribeEvent(healthEvent), background, foreground),
             new ListViewSubItem(null!, Cell(string.Join(", ", healthEvent.Culprits)), background, foreground)]);
    }

    private void RefreshCauses(HealthInfo info)
    {
        string signature = string.Join("|", info.Culprits.Select(culprit =>
            $"{culprit.ProcessName}:{culprit.ProcessCount}:{culprit.ToUsageText()}:{string.Join(",", culprit.Resources)}"));

        visibleCauses = info.Culprits;

        causesView.EmptyListViewText = info.Level >= HealthLevel.Warning ? NoProcessText : NoCausesText;

        if (signature == lastCausesSignature) {
            return;
        }

        lastCausesSignature = signature;

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;
        int   selected   = causesView.SelectedIndex;

        causesView.Items.Clear();

        foreach (HealthCulprit culprit in info.Culprits) {
            string name = culprit.ProcessCount > 1
                ? $"{culprit.ProcessName} ({culprit.ProcessCount})"
                : culprit.ProcessName;

            string behind = string.Join(", ", culprit.Resources.Select(ResourceName));

            causesView.Items.Add(new ListViewItem([name, culprit.ToUsageText(), behind], background, foreground));
        }

        if (causesView.Items.Count > 0) {
            causesView.SelectedIndex = Math.Clamp(selected, 0, causesView.Items.Count - 1);
        }

        causesView.FooterText = info.Culprits.Count > 0
            ? CommonControl.FooterText(appConfig.Theme, ShowProcessHotKey)
            : string.Empty;
    }

    private void RefreshEvents(HealthInfo info)
    {
        string events = string.Join("|", info.Events.Select(healthEvent =>
            $"{healthEvent.Resource}:{healthEvent.Level}:{healthEvent.StartUtc.Ticks}:{DescribeEvent(healthEvent)}"));

        string reports = string.Join("|", info.Reports.Select(report => $"{report.TimeUtc.Ticks}:{report.ToTitle()}:{report.ToDetailText()}"));
        string signature = $"{events}#{reports}";

        if (signature == lastEventsSignature) {
            return;
        }

        lastEventsSignature = signature;

        int selected = eventsView.SelectedIndex;

        eventsView.Items.Clear();

        IEnumerable<(DateTime TimeUtc, ListViewItem Row)> rows = info.Events
            .Select(healthEvent => (healthEvent.StartUtc, EventRow(healthEvent)))
            .Concat(info.Reports.Select(report => (report.TimeUtc, ReportRow(report))))
            .OrderByDescending(entry => entry.Item1);

        foreach ((_, ListViewItem row) in rows) {
            eventsView.Items.Add(row);
        }

        if (eventsView.Items.Count > 0) {
            eventsView.SelectedIndex = Math.Clamp(selected, 0, eventsView.Items.Count - 1);
        }
    }

    private void RefreshResources(HealthInfo info)
    {
        resourcesView.HeaderText = DescribeHeadline(info);

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        if (resourcesView.Items.Count != info.Resources.Count) {
            resourcesView.Items.Clear();

            foreach (HealthReading reading in info.Resources) {
                resourcesView.Items.Add(new ListViewItem(
                    [new ListViewSubItem(null!, ResourceName(reading.Resource), background, foreground),
                     new ListViewSubItem(null!, string.Empty, background, foreground),
                     new ListViewSubItem(null!, string.Empty, background, foreground),
                     new ListViewSubItem(null!, string.Empty, background, foreground),
                     new ListViewSubItem(null!, string.Empty, background, foreground)]));
            }
        }

        for (int i = 0; i < info.Resources.Count; i++) {
            HealthReading reading = info.Resources[i];
            ListViewItem  row     = resourcesView.Items[i];

            HealthCause? reason = reading.Level >= HealthLevel.Warning 
                ? reading.Cause 
                : null;

            string saturation = reason != null && !reason.IsShownInUtilisation()
                ? reason.ToReasonText()
                : reading.Measures.ToSaturationText();

            row.SubItems[1].Text            = StatusText(reading.Level);
            row.SubItems[1].ForegroundColor = StatusColour(reading.Level);
            row.SubItems[2].Text            = Cell(reading.Measures.ToUtilisationText());
            row.SubItems[3].Text            = Cell(saturation);
            row.SubItems[4].Text            = Cell(reading.Measures.ToErrorsText());
        }
    }

    private void RefreshRows(HealthInfo info)
    {
        RefreshResources(info);
        RefreshCauses(info);
        RefreshEvents(info);
    }

    private ListViewItem ReportRow(HealthReport report)
    {
        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        return new ListViewItem(
            [new ListViewSubItem(null!, DescribeTime(report.TimeUtc, DateTime.UtcNow), background, foreground),
             new ListViewSubItem(null!, report.ToTitle(), background, foreground),
             new ListViewSubItem(null!, report.ToDetailText(), background, foreground),
             new ListViewSubItem(null!, Cell(report.App), background, foreground)]);
    }

    internal static string ResourceName(HealthResource resource) => resource switch {
        HealthResource.Cpu => "CPU",
        HealthResource.Gpu => "GPU",
                         _ => resource.ToString()
    };

    private Color StatusColour(HealthLevel level) => level switch {
        HealthLevel.Ok       => appConfig.Theme.RangeLowBackground,
        HealthLevel.Warning  => appConfig.Theme.RangeMidBackground,
        HealthLevel.Critical => appConfig.Theme.RangeHighBackground,
                           _ => appConfig.Theme.ListViewForeground
    };

    private static string StatusText(HealthLevel level) => level switch {
        HealthLevel.Ok       => $"{StatusGlyph} OK",
        HealthLevel.Warning  => $"{StatusGlyph} Warning",
        HealthLevel.Critical => $"{StatusGlyph} Critical",
                           _ => EmptyCell
    };
}
