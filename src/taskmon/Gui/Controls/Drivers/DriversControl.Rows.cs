using System.Drawing;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Drivers;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.Gui.Controls.Drivers;

public sealed partial class DriversControl
{
    private const string NotAvailable = "N/A";

    private void RebuildRows(IReadOnlyList<DriverInfo> drivers)
    {
        driversView.Items.Clear();

        Color background = appConfig.DefaultTheme.Background;
        Color foreground = appConfig.DefaultTheme.Foreground;
        Color stoppedForeground = appConfig.DefaultTheme.RangeMidForeground;

        int running = 0;

        foreach (DriverInfo driver in drivers) {
            if (driver.Status == WindowsServiceStatus.Running) {
                running++;
            }

            Color statusForeground =
                driver.Status == WindowsServiceStatus.Running ? foreground : stoppedForeground;

            ListViewItem row = new(new[] {
                new ListViewSubItem(null!, Cell(driver.DisplayName), background, foreground),
                new ListViewSubItem(null!, Cell(driver.Version), background, foreground),
                new ListViewSubItem(null!, DescribeStatus(driver.Status), background, statusForeground),
                new ListViewSubItem(null!, DescribeStartType(driver), background, foreground),
                new ListViewSubItem(null!, Cell(driver.ImagePath), background, foreground)
            });

            driversView.Items.Add(row);
        }

        driversView.FooterText =
            $"{running} running / {drivers.Count} total     r Refresh     ↑ ↓ PgUp PgDn Scroll";
    }

    private void RebuildDetailRows(DriverInfo? driver)
    {
        detailView.Items.Clear();

        if (driver is null) {
            return;
        }

        Color background = appConfig.DefaultTheme.Background;
        Color foreground = appConfig.DefaultTheme.Foreground;
        Color stoppedForeground = appConfig.DefaultTheme.RangeMidForeground;

        Color statusForeground =
            driver.Status == WindowsServiceStatus.Running ? foreground : stoppedForeground;

        detailView.Items.Add(new ListViewItem(["Name", Cell(driver.DisplayName)], background, foreground));

        detailView.Items.Add(new ListViewItem(
            [new ListViewSubItem(null!, "Status", background, foreground),
             new ListViewSubItem(null!, DescribeStatus(driver.Status), background, statusForeground)]));

        detailView.Items.Add(new ListViewItem(["Startup Type", DescribeStartType(driver)], background, foreground));
        detailView.Items.Add(new ListViewItem(["Version", Cell(driver.Version)], background, foreground));
        detailView.Items.Add(new ListViewItem(["Path", Cell(driver.ImagePath)], background, foreground));
    }

    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;

    private static string DescribeStatus(WindowsServiceStatus status) => status switch {
        WindowsServiceStatus.Stopped => "Stopped",
        WindowsServiceStatus.StartPending => "Start Pending",
        WindowsServiceStatus.StopPending => "Stop Pending",
        WindowsServiceStatus.Running => "Running",
        WindowsServiceStatus.ContinuePending => "Continue Pending",
        WindowsServiceStatus.PausePending => "Pause Pending",
        WindowsServiceStatus.Paused => "Paused",
        _ => status.ToString()
    };

    private static string DescribeStartType(DriverInfo driver) => driver.StartType switch {
        WindowsServiceStartType.AutomaticStart =>
            driver.DelayedAutoStart ? "Automatic (Delayed Start)" : "Automatic",
        WindowsServiceStartType.ManualStart => "Manual",
        WindowsServiceStartType.Disabled => "Disabled",
        WindowsServiceStartType.BootStart => "Boot Start",
        WindowsServiceStartType.SystemStart => "System Start",
        _ => driver.StartType.ToString()
    };
}
