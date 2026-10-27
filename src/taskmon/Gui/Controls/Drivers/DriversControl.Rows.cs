using System.Drawing;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Drivers;

namespace Task.Monitor.Gui.Controls.Drivers;

public sealed partial class DriversControl
{
    private const string NotAvailable = "N/A";

    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;

    private static string DescribeStartType(DriverInfo driver) => driver.StartType switch {
        DriverStartType.AutomaticStart =>
            driver.DelayedAutoStart ? "Automatic (Delayed Start)" : "Automatic",
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

    // A running Windows driver or a loaded kext; anything else draws in the stopped colour.
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

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;
        Color stoppedForeground = appConfig.Theme.RangeMidForeground;

        Color statusForeground = IsActive(driver.Status) 
                ? foreground 
                : stoppedForeground;

        detailView.Items.Add(new ListViewItem(["Name", Cell(driver.DisplayName)], background, foreground));

        detailView.Items.Add(new ListViewItem(
            [new ListViewSubItem(null!, "Status",                      background, foreground),
             new ListViewSubItem(null!, DescribeStatus(driver.Status), background, statusForeground)]));

        detailView.Items.Add(new ListViewItem(["Startup Type", DescribeStartType(driver)], background, foreground));
        detailView.Items.Add(new ListViewItem(["Version",      Cell(driver.Version)],      background, foreground));
        detailView.Items.Add(new ListViewItem(["Publisher",    Cell(driver.Publisher)],    background, foreground));
        detailView.Items.Add(new ListViewItem(["Path",         Cell(driver.ImagePath)],    background, foreground));
    }

    private void RebuildRows(IReadOnlyList<DriverInfo> drivers)
    {
        driversView.Items.Clear();

        visibleDrivers = drivers.Where(MatchesFilter).ToList();

        Color background        = appConfig.Theme.ListViewBackground;
        Color foreground        = appConfig.Theme.ListViewForeground;
        Color stoppedForeground = appConfig.Theme.RangeMidForeground;

        int running = drivers.Count(driver => IsActive(driver.Status));

        foreach (DriverInfo driver in visibleDrivers) {
            Color statusForeground =
                IsActive(driver.Status) ? foreground : stoppedForeground;

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
        driversView.FooterText          =
            $"{running} running / {drivers.Count} total     r Refresh     {filter.FooterHint}     ↑ ↓ PgUp PgDn Scroll";
    }
}
