using System.Drawing;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.LaunchdServices;

namespace Task.Monitor.Gui.Controls.LaunchdServices;

public sealed partial class LaunchdServicesControl
{
    private const string NotAvailable = "N/A";

    private static readonly ListSortColumn<LaunchdServiceInfo>[] SortColumns = [
        ListSortColumn<LaunchdServiceInfo>.ByText("LABEL", 0, service => service.Label),
        ListSortColumn<LaunchdServiceInfo>.ByText("GROUP", 1, service => service.Group),
        ListSortColumn<LaunchdServiceInfo>.ByText("STATUS", 2, DescribeStatus),
        ListSortColumn<LaunchdServiceInfo>.ByText("START", 3, service => DescribeStartType(service.StartType)),
        ListSortColumn<LaunchdServiceInfo>.ByText("ENABLED", 4, service => service.Disabled ? "No" : "Yes"),
        ListSortColumn<LaunchdServiceInfo>.ByText("PATH", 5, service => service.ProgramPath)
    ];

    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;

    private static string DescribeLastExit(int? status) => status switch {
        null                               => NotAvailable,
        { } value when (value & 0x7F) != 0 => $"Signal {value & 0x7F}",
        { } value                          => $"Exit {(value >> 8) & 0xFF}"
    };

    private static string DescribeStartType(LaunchdStartType startType) => startType switch {
        LaunchdStartType.KeepAlive => "Keep Alive",
        LaunchdStartType.AtLoad    => "At Load",
        LaunchdStartType.OnDemand  => "On Demand",
                                 _ => startType.ToString()
    };

    private static string DescribeStatus(LaunchdServiceInfo service) => service.Status switch {
        LaunchdServiceStatus.Running   => $"Running ({service.Pid})",
        LaunchdServiceStatus.Waiting   => "Waiting",
        LaunchdServiceStatus.NotLoaded => "Not Loaded",
        LaunchdServiceStatus.Unknown   => "Unknown",
                                     _ => service.Status.ToString()
    };

    private static bool HasFailed(LaunchdServiceInfo service) =>
        service.Status != LaunchdServiceStatus.Running &&
        service.LastExitStatus is { } status &&
        (status & 0x7F) == 0 &&
        (status >> 8) != 0;

    private bool MatchesFilter(LaunchdServiceInfo service) =>
        (int.TryParse(filter.Text, out int pid) && service.Pid == pid) ||
        filter.Matches(service.Label, service.Group, service.ProgramPath, service.Publisher);

    private void RebuildDetailRows(LaunchdServiceInfo? service)
    {
        detailView.Items.Clear();

        if (service is null) {
            return;
        }

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        Color lastExitForeground = HasFailed(service) ? appConfig.Theme.RangeHighForeground : foreground;

        detailView.Items.Add(new ListViewItem(["Label",      service.Label],             background, foreground));
        detailView.Items.Add(new ListViewItem(["Domain",     service.Domain.ToString()], background, foreground));
        detailView.Items.Add(new ListViewItem(["Group",      service.Group],             background, foreground));

        detailView.Items.Add(new ListViewItem(
            [new ListViewSubItem(null!, "Status",                 background, foreground),
             new ListViewSubItem(null!, DescribeStatus(service),  background, StatusForeground(service))]));

        detailView.Items.Add(new ListViewItem(["Start Type", DescribeStartType(service.StartType)], background, foreground));
        detailView.Items.Add(new ListViewItem(["Enabled",    service.Disabled ? "No" : "Yes"],      background, foreground));

        detailView.Items.Add(new ListViewItem(
            [new ListViewSubItem(null!, "Last Exit",                              background, foreground),
             new ListViewSubItem(null!, DescribeLastExit(service.LastExitStatus), background, lastExitForeground)]));

        detailView.Items.Add(new ListViewItem(["Program",    Cell(service.ProgramPath)], background, foreground));
        detailView.Items.Add(new ListViewItem(["Arguments",  Cell(service.Arguments)],   background, foreground));
        detailView.Items.Add(new ListViewItem(["Publisher",  Cell(service.Publisher)],   background, foreground));
        detailView.Items.Add(new ListViewItem(["Plist",      Cell(service.PlistPath)],   background, foreground));
    }

    private void RebuildRows(IReadOnlyList<LaunchdServiceInfo> entries)
    {
        servicesView.Items.Clear();

        visibleServices = sorter.Sort(entries.Where(MatchesFilter));

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        int running = entries.Count(service => service.Status == LaunchdServiceStatus.Running);

        foreach (LaunchdServiceInfo service in visibleServices) {
            ListViewItem row = new(new[] {
                new ListViewSubItem(null!, service.Label,                         background, foreground),
                new ListViewSubItem(null!, service.Group,                         background, foreground),
                new ListViewSubItem(null!, DescribeStatus(service),               background, StatusForeground(service)),
                new ListViewSubItem(null!, DescribeStartType(service.StartType),  background, foreground),
                new ListViewSubItem(null!, service.Disabled ? "No" : "Yes",       background, foreground),
                new ListViewSubItem(null!, Cell(service.ProgramPath),             background, foreground)
            });

            servicesView.Items.Add(row);
        }

        servicesView.EmptyListViewText   = filter.IsActive ? ListFilter.NoMatchesText : GatheringText;
        servicesView.HeaderHighlightText = filter.BuildHeaderText(visibleServices.Count, entries.Count);
        servicesView.HeaderText          = $"{running} running / {entries.Count} total";
        servicesView.FooterText          = CommonControl.FooterText(
            appConfig.Theme,
            [CommonControl.RefreshHotKey, CommonControl.SortHotKey, .. filter.FooterHotKeys, CommonControl.ScrollHotKey]);
    }

    private Color StatusForeground(LaunchdServiceInfo service) => service.Status switch {
        _ when HasFailed(service)        => appConfig.Theme.RangeHighForeground,
        LaunchdServiceStatus.Running or
        LaunchdServiceStatus.Waiting     => appConfig.Theme.ListViewForeground,
                                       _ => appConfig.Theme.RangeMidForeground
    };
}
