using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Drivers;

namespace Task.Monitor.Gui.Controls.Drivers;

public sealed partial class DriversControl
{
    private const string NotAvailable = "N/A";

    private static readonly ListSortColumn<DriverInfo>[] SortColumns = [
        ListSortColumn<DriverInfo>.ByText("NAME", 0, driver => driver.DisplayName),
        ListSortColumn<DriverInfo>.ByVersion("VERSION", 1, driver => driver.Version),
        ListSortColumn<DriverInfo>.ByText("STATUS", 2, driver => DescribeStatus(driver.Status)),
        ListSortColumn<DriverInfo>.ByText("START TYPE", 3, DescribeStartType),
        ListSortColumn<DriverInfo>.ByText("PATH", 4, driver => driver.ImagePath)
    ];

    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;

    private static string DescribeStartType(DriverInfo driver) => driver.StartType switch {
        DriverStartType.AutomaticStart => driver.DelayedAutoStart ? "Automatic (Delayed Start)" : "Automatic",
        DriverStartType.ManualStart    => "Manual",
        DriverStartType.Disabled       => "Disabled",
        DriverStartType.BootStart      => "Boot Start",
        DriverStartType.SystemStart    => "System Start",
        DriverStartType.OnDemand       => "On Demand",
                                     _ => driver.StartType.ToString()
    };

    private static string DescribeStatus(DriverStatus status) => status switch {
        DriverStatus.Stopped         => "Stopped",
        DriverStatus.StartPending    => "Start Pending",
        DriverStatus.StopPending     => "Stop Pending",
        DriverStatus.Running         => "Running",
        DriverStatus.ContinuePending => "Continue Pending",
        DriverStatus.PausePending    => "Pause Pending",
        DriverStatus.Paused          => "Paused",
        DriverStatus.Loaded          => "Loaded",
        DriverStatus.NotLoaded       => "Not Loaded",
                                   _ => status.ToString()
    };

    private static bool IsActive(DriverStatus status) =>
        status is DriverStatus.Running or DriverStatus.Loaded;

    private bool MatchesFilter(DriverInfo driver) =>
        filter.Matches(driver.Name, driver.DisplayName, driver.Publisher, driver.ImagePath);

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

        detailView.Items.Add(CommonControl.PropertyRow("Name",         Cell(driver.DisplayName),      theme));
        detailView.Items.Add(CommonControl.PropertyRow("Status",       DescribeStatus(driver.Status), theme, statusForeground));
        detailView.Items.Add(CommonControl.PropertyRow("Startup Type", DescribeStartType(driver),     theme));
        detailView.Items.Add(CommonControl.PropertyRow("Version",      Cell(driver.Version),          theme));
        detailView.Items.Add(CommonControl.PropertyRow("Publisher",    Cell(driver.Publisher),        theme));
        detailView.Items.Add(CommonControl.PropertyRow("Path",         Cell(driver.ImagePath),        theme));
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
        driversView.HeaderText          = $"{running} running / {drivers.Count} total";
        driversView.FooterText          = CommonControl.FooterText(
            appConfig.Theme,
            [CommonControl.RefreshHotKey, CommonControl.SortHotKey, .. filter.FooterHotKeys, CommonControl.ScrollHotKey]);
    }
}
