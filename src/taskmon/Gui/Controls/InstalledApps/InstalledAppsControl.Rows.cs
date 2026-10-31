using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.InstalledApps;

namespace Task.Monitor.Gui.Controls.InstalledApps;

public sealed partial class InstalledAppsControl
{
    private const string NotAvailable = "N/A";

    private static readonly ListSortColumn<InstalledApp>[] SortColumns = [
        ListSortColumn<InstalledApp>.ByText("NAME", 0, app => app.Name),
        ListSortColumn<InstalledApp>.ByVersion("VERSION", 1, app => app.Version),
        ListSortColumn<InstalledApp>.ByText("PUBLISHER", 2, app => app.Publisher),
        ListSortColumn<InstalledApp>.ByText("SCOPE", 3, app => DescribeScope(app.Scope)),
        ListSortColumn<InstalledApp>.ByValue<DateTime>("INSTALLED", 4, app => app.InstallDate),
        ListSortColumn<InstalledApp>.ByValue<long>("SIZE", 5, app => app.EstimatedSizeKb),
        ListSortColumn<InstalledApp>.ByText("LOCATION", 6, app => app.InstallLocation)
    ];
    private const string Measuring    = "...";

    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) 
            ? NotAvailable 
            : value;

    private static string DescribeInstallDate(DateTime? installDate) =>
        installDate is { } date 
            ? date.ToString("yyyy-MM-dd") 
            : NotAvailable;

    private static string DescribeScope(InstalledAppScope scope) =>
        scope == InstalledAppScope.Machine 
            ? "Machine" 
            : "User";

    // On macOS a bundle's size is measured after the scan, so it reads "..." until then.
    private static string DescribeSize(InstalledApp app) => app switch {
        { SizePending: true }           => Measuring,
        { EstimatedSizeKb: { } sizeKb } => (sizeKb * 1024L).ToFormattedByteSize(),
                                      _ => NotAvailable
    };

    private bool MatchesFilter(InstalledApp app) =>
        filter.Matches(app.Name, app.Publisher, app.Version, app.InstallLocation);

    private void RebuildDetailRows(InstalledApp? app)
    {
        detailView.Items.Clear();

        if (app is null) {
            return;
        }

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        detailView.Items.Add(new ListViewItem(["Name",      Cell(app.Name)],                       background, foreground));
        detailView.Items.Add(new ListViewItem(["Version",   Cell(app.Version)],                    background, foreground));
        detailView.Items.Add(new ListViewItem(["Publisher", Cell(app.Publisher)],                  background, foreground));
        detailView.Items.Add(new ListViewItem(["Scope",     DescribeScope(app.Scope)],             background, foreground));
        detailView.Items.Add(new ListViewItem(["Installed", DescribeInstallDate(app.InstallDate)], background, foreground));
        detailView.Items.Add(new ListViewItem(["Size",      DescribeSize(app)],                    background, foreground));
        detailView.Items.Add(new ListViewItem(["Location",  Cell(app.InstallLocation)],            background, foreground));
    }

    private void RebuildRows(IReadOnlyList<InstalledApp> apps)
    {
        installedAppsView.Items.Clear();

        visibleApps = sorter.Sort(apps.Where(MatchesFilter));

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        foreach (InstalledApp app in visibleApps) {
            ListViewItem row = new(new[] {
                new ListViewSubItem(null!, Cell(app.Name),                       background, foreground),
                new ListViewSubItem(null!, Cell(app.Version),                    background, foreground),
                new ListViewSubItem(null!, Cell(app.Publisher),                  background, foreground),
                new ListViewSubItem(null!, DescribeScope(app.Scope),             background, foreground),
                new ListViewSubItem(null!, DescribeInstallDate(app.InstallDate), background, foreground),
                new ListViewSubItem(null!, DescribeSize(app),                    background, foreground),
                new ListViewSubItem(null!, Cell(app.InstallLocation),            background, foreground)
            });

            installedAppsView.Items.Add(row);
        }

        installedAppsView.EmptyListViewText   = filter.IsActive ? ListFilter.NoMatchesText : GatheringText;
        installedAppsView.HeaderHighlightText = filter.BuildHeaderText(visibleApps.Count, apps.Count);
        installedAppsView.HeaderText          = $"{apps.Count} installed";
        installedAppsView.FooterText          = CommonControl.FooterText(
            appConfig.Theme,
            [CommonControl.RefreshHotKey, CommonControl.SortHotKey, .. filter.FooterHotKeys, CommonControl.ScrollHotKey]);
    }
}
