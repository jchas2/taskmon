using System.Drawing;
using System.Globalization;
using Task.Monitor.Configuration;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.LaunchdServices;

namespace Task.Monitor.Gui.Controls.LaunchdServices;

public sealed partial class LaunchdServicesControl
{
    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;

    private static string DescribeLastExit(int? status) => status switch {
        null                               => NotAvailable,
        { } value when (value & 0x7F) != 0 => string.Format(CultureInfo.CurrentCulture, Strings.LaunchdSignal, value & 0x7F),
        { } value                          => string.Format(CultureInfo.CurrentCulture, Strings.LaunchdExit, (value >> 8) & 0xFF)
    };

    private static string DescribeStartType(LaunchdStartType startType) => startType switch {
        LaunchdStartType.KeepAlive => Strings.LaunchdStartKeepAlive,
        LaunchdStartType.AtLoad    => Strings.LaunchdStartAtLoad,
        LaunchdStartType.OnDemand  => Strings.StartTypeOnDemand,
                                 _ => startType.ToString()
    };

    private static string DescribeStatus(LaunchdServiceInfo service) => service.Status switch {
        LaunchdServiceStatus.Running   => string.Format(CultureInfo.CurrentCulture, Strings.LaunchdStatusRunning, service.Pid),
        LaunchdServiceStatus.Waiting   => Strings.LaunchdStatusWaiting,
        LaunchdServiceStatus.NotLoaded => Strings.StatusNotLoaded,
        LaunchdServiceStatus.Unknown   => Strings.CommonUnknown,
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

    private static string NotAvailable => Strings.CommonNotAvailable;

    private void RebuildDetailRows(LaunchdServiceInfo? service)
    {
        detailView.Items.Clear();

        if (service is null) {
            return;
        }

        Theme theme = appConfig.Theme;

        Color lastExitForeground = HasFailed(service) ? theme.RangeHighBackground : theme.PropertyValue;

        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailLabel,      service.Label,                          theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailDomain, service.Domain.ToDisplayText(),              theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailGroup,      service.Group,                          theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailStatus,     DescribeStatus(service),                theme, StatusForeground(service, theme.PropertyValue)));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailStartType, DescribeStartType(service.StartType),   theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailEnabled,    service.Disabled ? Strings.CommonNo : Strings.CommonYes,        theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailLastExit,  DescribeLastExit(service.LastExitStatus), theme, lastExitForeground));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailProgram,    Cell(service.ProgramPath),              theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailArguments,  Cell(service.Arguments),                theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailPublisher,  Cell(service.Publisher),                theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailPlist,      Cell(service.PlistPath),                theme));
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
                new ListViewSubItem(null!, DescribeStatus(service),               background, StatusForeground(service, foreground)),
                new ListViewSubItem(null!, DescribeStartType(service.StartType),  background, foreground),
                new ListViewSubItem(null!, service.Disabled ? Strings.CommonNo : Strings.CommonYes,       background, foreground),
                new ListViewSubItem(null!, Cell(service.ProgramPath),             background, foreground)
            });

            servicesView.Items.Add(row);
        }

        servicesView.EmptyListViewText   = filter.IsActive ? ListFilter.NoMatchesText : GatheringText;
        servicesView.HeaderHighlightText = filter.BuildHeaderText(visibleServices.Count, entries.Count);
        servicesView.HeaderText          = string.Format(CultureInfo.CurrentCulture, Strings.ListHeaderRunningTotal, running, entries.Count);
        servicesView.FooterText          = CommonControl.FooterText(
            appConfig.Theme,
            [CommonControl.RefreshHotKey, CommonControl.SortHotKey, .. filter.FooterHotKeys, CommonControl.ScrollHotKey]);
    }

    private static ListSortColumn<LaunchdServiceInfo>[] SortColumns => [
        ListSortColumn<LaunchdServiceInfo>.ByText(Strings.LaunchdColumnLabel, 0, service => service.Label),
        ListSortColumn<LaunchdServiceInfo>.ByText(Strings.LaunchdColumnGroup, 1, service => service.Group),
        ListSortColumn<LaunchdServiceInfo>.ByText(Strings.ListColumnStatus, 2, DescribeStatus),
        ListSortColumn<LaunchdServiceInfo>.ByText(Strings.LaunchdColumnStart, 3, service => DescribeStartType(service.StartType)),
        ListSortColumn<LaunchdServiceInfo>.ByText(Strings.LaunchdColumnEnabled, 4, service => service.Disabled ? Strings.CommonNo : Strings.CommonYes),
        ListSortColumn<LaunchdServiceInfo>.ByText(Strings.ListColumnPath, 5, service => service.ProgramPath)
    ];

    private Color StatusForeground(LaunchdServiceInfo service, Color normalForeground) => service.Status switch {
        _ when HasFailed(service)        => appConfig.Theme.RangeHighBackground,
        LaunchdServiceStatus.Running or
        LaunchdServiceStatus.Waiting     => normalForeground,
                                       _ => appConfig.Theme.RangeMidBackground
    };
}
