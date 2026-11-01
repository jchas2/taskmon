using System.Drawing;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Health;

namespace Task.Monitor.Gui.Controls.Health;

public sealed partial class HealthControl
{
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
        string duration = healthEvent.EndUtc is { } end
            ? DescribeDuration(end - healthEvent.StartUtc)
            : "ongoing";

        return string.IsNullOrEmpty(healthEvent.PeakReason)
            ? duration
            : $"{duration}, peak {healthEvent.PeakReason}";
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

    private void RefreshCauses(HealthInfo info)
    {
        string signature = string.Join("|", info.Culprits.Select(culprit =>
            $"{culprit.ProcessName}:{culprit.ProcessCount}:{culprit.Detail}:{string.Join(",", culprit.Resources)}"));

        visibleCauses = info.Culprits;

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

            causesView.Items.Add(new ListViewItem([name, culprit.Detail, behind], background, foreground));
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
        string signature = string.Join("|", info.Events.Select(healthEvent =>
            $"{healthEvent.Resource}:{healthEvent.Level}:{healthEvent.StartUtc.Ticks}:{healthEvent.PeakReason}:{DescribeEvent(healthEvent)}"));

        if (signature == lastEventsSignature) {
            return;
        }

        lastEventsSignature = signature;

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;
        int   selected   = eventsView.SelectedIndex;

        eventsView.Items.Clear();

        foreach (HealthEvent healthEvent in info.Events) {
            eventsView.Items.Add(new ListViewItem(
                [new ListViewSubItem(null!, $"{healthEvent.StartUtc.ToLocalTime():HH:mm:ss}", background, foreground),
                 new ListViewSubItem(null!, $"{ResourceName(healthEvent.Resource)} → {healthEvent.Level}", background, StatusColour(healthEvent.Level)),
                 new ListViewSubItem(null!, DescribeEvent(healthEvent), background, foreground),
                 new ListViewSubItem(null!, Cell(string.Join(", ", healthEvent.Culprits)), background, foreground)]));
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

            row.SubItems[1].Text            = StatusText(reading.Level);
            row.SubItems[1].ForegroundColor = StatusColour(reading.Level);
            row.SubItems[2].Text            = Cell(reading.Utilisation);
            row.SubItems[3].Text            = Cell(reading.Level >= HealthLevel.Warning ? reading.Reason : reading.Saturation);
            row.SubItems[4].Text            = Cell(reading.Errors);
        }
    }

    private void RefreshRows(HealthInfo info)
    {
        RefreshResources(info);
        RefreshCauses(info);
        RefreshEvents(info);
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
