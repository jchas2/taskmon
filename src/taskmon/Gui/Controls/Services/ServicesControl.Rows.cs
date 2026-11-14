using System.Drawing;
using System.Globalization;
using Task.Monitor.Configuration;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.Gui.Controls.Services;

public sealed partial class ServicesControl
{
    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;

    private static string DescribeLogOnAs(string? logOnAs)
    {
        if (string.IsNullOrEmpty(logOnAs)) {
            return Strings.ServicesLocalSystem;
        }

        if (logOnAs.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase)) {
            return Strings.ServicesLocalSystem;
        }

        if (logOnAs.Equals(@"NT AUTHORITY\LocalService", StringComparison.OrdinalIgnoreCase)) {
            return Strings.ServicesLocalService;
        }

        if (logOnAs.Equals(@"NT AUTHORITY\NetworkService", StringComparison.OrdinalIgnoreCase)) {
            return Strings.ServicesNetworkService;
        }

        return logOnAs;
    }

    private static string DescribeStartType(WindowsServiceInfo service) => service.StartType switch {
        WindowsServiceStartType.AutomaticStart => service.DelayedAutoStart ? Strings.StartTypeAutomaticDelayed : Strings.StartTypeAutomatic,
        WindowsServiceStartType.ManualStart    => Strings.StartTypeManual,
        WindowsServiceStartType.Disabled       => Strings.StartTypeDisabled,
        WindowsServiceStartType.BootStart      => Strings.StartTypeBootStart,
        WindowsServiceStartType.SystemStart    => Strings.StartTypeSystemStart,
                                             _ => service.StartType.ToString()
    };

    private static string DescribeStatus(WindowsServiceStatus status) => status switch {
        WindowsServiceStatus.Stopped         => Strings.ServiceStatusStopped,
        WindowsServiceStatus.StartPending    => Strings.ServiceStatusStartPending,
        WindowsServiceStatus.StopPending     => Strings.ServiceStatusStopPending,
        WindowsServiceStatus.Running         => Strings.ServiceStatusRunning,
        WindowsServiceStatus.ContinuePending => Strings.ServiceStatusContinuePending,
        WindowsServiceStatus.PausePending    => Strings.ServiceStatusPausePending,
        WindowsServiceStatus.Paused          => Strings.ServiceStatusPaused,
                                           _ => status.ToString()
    };

    private bool MatchesFilter(WindowsServiceInfo service) =>
        filter.Matches(service.ServiceName, service.DisplayName, service.Description, service.LogOnAs);

    private static string NotAvailable => Strings.CommonNotAvailable;

    private void RebuildDetailRows(WindowsServiceInfo? service)
    {
        detailView.Items.Clear();

        if (service is null) {
            return;
        }

        Theme theme = appConfig.Theme;

        Color statusForeground = service.Status == WindowsServiceStatus.Running
            ? theme.RangeLowBackground
            : theme.RangeMidBackground;

        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailName,         Cell(service.DisplayName),        theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailStatus,       StatusText(service),              theme, statusForeground));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailStartupType, DescribeStartType(service),       theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailLogOnAs,    DescribeLogOnAs(service.LogOnAs), theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailDescription,  Cell(service.Description),        theme));
    }

    private void RebuildRows(IReadOnlyList<WindowsServiceInfo> services)
    {
        int     topIndex      = servicesView.TopIndex;
        int     selectedIndex = servicesView.SelectedIndex;
        string? selectedName  = selectedIndex >= 0 && selectedIndex < visibleServices.Count
            ? visibleServices[selectedIndex].ServiceName
            : null;

        servicesView.Items.Clear();

        visibleServices = sorter.Sort(services.Where(MatchesFilter));

        Color background        = appConfig.Theme.ListViewBackground;
        Color foreground        = appConfig.Theme.ListViewForeground;
        Color runningForeground = appConfig.Theme.RangeLowBackground;
        Color stoppedForeground = appConfig.Theme.RangeMidBackground;

        int running = services.Count(service => service.Status == WindowsServiceStatus.Running);

        foreach (WindowsServiceInfo service in visibleServices) {
            Color statusForeground =
                service.Status == WindowsServiceStatus.Running ? runningForeground : stoppedForeground;

            ListViewItem row = new(new[] {
                new ListViewSubItem(null!, Cell(service.DisplayName),        background, foreground),
                new ListViewSubItem(null!, StatusText(service),              background, statusForeground),
                new ListViewSubItem(null!, DescribeStartType(service),       background, foreground),
                new ListViewSubItem(null!, DescribeLogOnAs(service.LogOnAs), background, foreground),
                new ListViewSubItem(null!, Cell(service.Description),        background, foreground)
            }) {
                Checked = inFlight.TryGetValue(service.ServiceName, out bool start)
                    ? start
                    : ShowsAsRunning(service.Status)
            };

            servicesView.Items.Add(row);
        }

        SelectFollowedService(selectedName, selectedIndex, topIndex);

        servicesView.EmptyListViewText   = filter.IsActive ? ListFilter.NoMatchesText : GatheringText;
        servicesView.HeaderHighlightText = filter.BuildHeaderText(visibleServices.Count, services.Count);
        servicesView.HeaderText          = string.Format(CultureInfo.CurrentCulture, Strings.ListHeaderRunningTotal, running, services.Count);
        servicesView.FooterText          = CommonControl.FooterText(
            appConfig.Theme,
            [
                .. CanControl ? [ToggleHotKey] : Array.Empty<(string, string)>(),
                CommonControl.RefreshHotKey,
                CommonControl.SortHotKey,
                .. filter.FooterHotKeys,
                CommonControl.ScrollHotKey
            ]);
    }

    private void SelectFollowedService(string? serviceName, int previousIndex, int topIndex)
    {
        if (visibleServices.Count == 0) {
            return;
        }

        int index = -1;

        if (serviceName is not null) {
            for (int i = 0; i < visibleServices.Count; i++) {
                if (string.Equals(visibleServices[i].ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)) {
                    index = i;
                    break;
                }
            }
        }

        if (index < 0) {
            index = Math.Clamp(previousIndex, 0, visibleServices.Count - 1);
        }

        servicesView.TopIndex      = topIndex;
        servicesView.SelectedIndex = index;
        servicesView.ScrollSelectedIntoView();
    }

    private static ListSortColumn<WindowsServiceInfo>[] SortColumns => [
        ListSortColumn<WindowsServiceInfo>.ByText(Strings.ListColumnName, 0, service => service.DisplayName),
        ListSortColumn<WindowsServiceInfo>.ByText(Strings.ListColumnStatus, 1, service => DescribeStatus(service.Status)),
        ListSortColumn<WindowsServiceInfo>.ByText(Strings.ServicesColumnStartupType, 2, DescribeStartType),
        ListSortColumn<WindowsServiceInfo>.ByText(Strings.ServicesColumnLogOnAs, 3, service => DescribeLogOnAs(service.LogOnAs)),
        ListSortColumn<WindowsServiceInfo>.ByText(Strings.ListColumnDescription, 4, service => service.Description)
    ];

    private string StatusText(WindowsServiceInfo service) =>
        inFlight.TryGetValue(service.ServiceName, out bool start)
            ? start ? Strings.ServicesStarting : Strings.ServicesStopping
            : DescribeStatus(service.Status);
}
