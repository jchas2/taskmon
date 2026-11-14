using System.Drawing;
using System.Globalization;
using Task.Monitor.Configuration;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Drivers;

namespace Task.Monitor.Gui.Controls.Drivers;

public sealed partial class DriversControl
{
    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;

    private static string DescribeStartType(DriverInfo driver) => driver.StartType switch {
        DriverStartType.AutomaticStart => driver.DelayedAutoStart ? Strings.StartTypeAutomaticDelayed : Strings.StartTypeAutomatic,
        DriverStartType.ManualStart    => Strings.StartTypeManual,
        DriverStartType.Disabled       => Strings.StartTypeDisabled,
        DriverStartType.BootStart      => Strings.StartTypeBootStart,
        DriverStartType.SystemStart    => Strings.StartTypeSystemStart,
        DriverStartType.OnDemand       => Strings.StartTypeOnDemand,
                                     _ => driver.StartType.ToString()
    };

    private static string DescribeStatus(DriverStatus status) => status switch {
        DriverStatus.Stopped         => Strings.ServiceStatusStopped,
        DriverStatus.StartPending    => Strings.ServiceStatusStartPending,
        DriverStatus.StopPending     => Strings.ServiceStatusStopPending,
        DriverStatus.Running         => Strings.ServiceStatusRunning,
        DriverStatus.ContinuePending => Strings.ServiceStatusContinuePending,
        DriverStatus.PausePending    => Strings.ServiceStatusPausePending,
        DriverStatus.Paused          => Strings.ServiceStatusPaused,
        DriverStatus.Loaded          => Strings.DriverStatusLoaded,
        DriverStatus.NotLoaded       => Strings.StatusNotLoaded,
                                   _ => status.ToString()
    };

    private static bool IsActive(DriverStatus status) =>
        status is DriverStatus.Running or DriverStatus.Loaded;

    private bool MatchesFilter(DriverInfo driver) =>
        filter.Matches(driver.Name, driver.DisplayName, driver.Publisher, driver.ImagePath);

    private static string NotAvailable => Strings.CommonNotAvailable;

    private void RebuildDetailRows(DriverInfo? driver)
    {
        detailView.Items.Clear();

        if (driver is null) {
            return;
        }

        Theme theme = appConfig.Theme;

        Color statusForeground = IsActive(driver.Status)
            ? theme.RangeLowBackground
            : theme.RangeMidBackground;

        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailName,         Cell(driver.DisplayName),      theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailStatus,       DescribeStatus(driver.Status), theme, statusForeground));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailStartupType, DescribeStartType(driver),     theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailVersion,      Cell(driver.Version),          theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailPublisher,    Cell(driver.Publisher),        theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailPath,         Cell(driver.ImagePath),        theme));
    }

    private void RebuildRows(IReadOnlyList<DriverInfo> drivers)
    {
        driversView.Items.Clear();

        visibleDrivers = sorter.Sort(drivers.Where(MatchesFilter));

        Color background        = appConfig.Theme.ListViewBackground;
        Color foreground        = appConfig.Theme.ListViewForeground;
        Color runningForeground = appConfig.Theme.RangeLowBackground;
        Color stoppedForeground = appConfig.Theme.RangeMidBackground;

        int running = drivers.Count(driver => IsActive(driver.Status));

        foreach (DriverInfo driver in visibleDrivers) {
            Color statusForeground =
                IsActive(driver.Status) ? runningForeground : stoppedForeground;

            ListViewItem row = new(new[] {
                new ListViewSubItem(null!, Cell(driver.DisplayName),      background, foreground),
                new ListViewSubItem(null!, Cell(driver.Version),          background, foreground),
                new ListViewSubItem(null!, DescribeStatus(driver.Status), background, statusForeground),
                new ListViewSubItem(null!, DescribeStartType(driver),     background, foreground),
                new ListViewSubItem(null!, Cell(driver.ImagePath),        background, foreground)
            });

            driversView.Items.Add(row);
        }

        driversView.EmptyListViewText   = filter.IsActive ? ListFilter.NoMatchesText : GatheringText;
        driversView.HeaderHighlightText = filter.BuildHeaderText(visibleDrivers.Count, drivers.Count);
        driversView.HeaderText          = string.Format(CultureInfo.CurrentCulture, Strings.ListHeaderRunningTotal, running, drivers.Count);
        driversView.FooterText          = CommonControl.FooterText(
            appConfig.Theme,
            [CommonControl.RefreshHotKey, CommonControl.SortHotKey, .. filter.FooterHotKeys, CommonControl.ScrollHotKey]);
    }

    private static ListSortColumn<DriverInfo>[] SortColumns => [
        ListSortColumn<DriverInfo>.ByText(Strings.ListColumnName, 0, driver => driver.DisplayName),
        ListSortColumn<DriverInfo>.ByVersion(Strings.ListColumnVersion, 1, driver => driver.Version),
        ListSortColumn<DriverInfo>.ByText(Strings.ListColumnStatus, 2, driver => DescribeStatus(driver.Status)),
        ListSortColumn<DriverInfo>.ByText(Strings.DriversColumnStartType, 3, DescribeStartType),
        ListSortColumn<DriverInfo>.ByText(Strings.ListColumnPath, 4, driver => driver.ImagePath)
    ];
}
