using System.Drawing;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.Gui.Controls.Services;

public sealed partial class ServicesControl
{
    private const string NotAvailable = "N/A";

    private void RebuildRows(IReadOnlyList<WindowsServiceInfo> services)
    {
        servicesView.Items.Clear();

        Color background = appConfig.DefaultTheme.Background;
        Color foreground = appConfig.DefaultTheme.Foreground;
        Color stoppedForeground = appConfig.DefaultTheme.RangeMidForeground;

        int running = 0;

        foreach (WindowsServiceInfo service in services) {
            if (service.Status == WindowsServiceStatus.Running) {
                running++;
            }

            Color statusForeground =
                service.Status == WindowsServiceStatus.Running ? foreground : stoppedForeground;

            ListViewItem row = new(new[] {
                new ListViewSubItem(null!, Cell(service.DisplayName), background, foreground),
                new ListViewSubItem(null!, DescribeStatus(service.Status), background, statusForeground),
                new ListViewSubItem(null!, DescribeStartType(service), background, foreground),
                new ListViewSubItem(null!, DescribeLogOnAs(service.LogOnAs), background, foreground),
                new ListViewSubItem(null!, Cell(service.Description), background, foreground)
            });

            servicesView.Items.Add(row);
        }

        servicesView.FooterText =
            $"{running} running / {services.Count} total     r Refresh     ↑ ↓ PgUp PgDn Scroll";
    }

    private void RebuildDetailRows(WindowsServiceInfo? service)
    {
        detailView.Items.Clear();

        if (service is null) {
            return;
        }

        Color background = appConfig.DefaultTheme.Background;
        Color foreground = appConfig.DefaultTheme.Foreground;
        Color stoppedForeground = appConfig.DefaultTheme.RangeMidForeground;

        Color statusForeground =
            service.Status == WindowsServiceStatus.Running ? foreground : stoppedForeground;

        detailView.Items.Add(new ListViewItem(["Name", Cell(service.DisplayName)], background, foreground));

        detailView.Items.Add(new ListViewItem(
            [new ListViewSubItem(null!, "Status", background, foreground),
             new ListViewSubItem(null!, DescribeStatus(service.Status), background, statusForeground)]));

        detailView.Items.Add(new ListViewItem(["Startup Type", DescribeStartType(service)], background, foreground));
        detailView.Items.Add(new ListViewItem(["Log On As", DescribeLogOnAs(service.LogOnAs)], background, foreground));
        detailView.Items.Add(new ListViewItem(["Description", Cell(service.Description)], background, foreground));
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

    private static string DescribeStartType(WindowsServiceInfo service) => service.StartType switch {
        WindowsServiceStartType.AutomaticStart =>
            service.DelayedAutoStart ? "Automatic (Delayed Start)" : "Automatic",
        WindowsServiceStartType.ManualStart => "Manual",
        WindowsServiceStartType.Disabled => "Disabled",
        WindowsServiceStartType.BootStart => "Boot Start",
        WindowsServiceStartType.SystemStart => "System Start",
        _ => service.StartType.ToString()
    };

    // Turns the well-known built-in accounts into the names the Services snap-in shows; anything
    // else (a named user or a domain account) is shown exactly as the Service Control Manager
    // stores it. An absent value means the service did not specify one, which the OS treats as
    // LocalSystem.
    private static string DescribeLogOnAs(string? logOnAs)
    {
        if (string.IsNullOrEmpty(logOnAs)) {
            return "Local System";
        }

        // The Service Control Manager is inconsistent about casing here - some services store
        // "NT AUTHORITY\LocalService", others "NT Authority\LocalService" - so match ignoring case
        // rather than switching on the literal string.
        if (logOnAs.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase)) {
            return "Local System";
        }

        if (logOnAs.Equals(@"NT AUTHORITY\LocalService", StringComparison.OrdinalIgnoreCase)) {
            return "Local Service";
        }

        if (logOnAs.Equals(@"NT AUTHORITY\NetworkService", StringComparison.OrdinalIgnoreCase)) {
            return "Network Service";
        }

        return logOnAs;
    }
}
