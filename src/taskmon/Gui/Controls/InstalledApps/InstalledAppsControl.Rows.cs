using System.Drawing;
using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.InstalledApps;

namespace Task.Monitor.Gui.Controls.InstalledApps;

public sealed partial class InstalledAppsControl
{
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
            ? Strings.ScopeMachine
            : Strings.ScopeUser;

    private static string DescribeSize(InstalledApp app) => app switch {
        { SizePending: true }           => Measuring,
        { EstimatedSizeKb: { } sizeKb } => (sizeKb * 1024L).ToFormattedByteSize(),
                                      _ => NotAvailable
    };

    private bool MatchesFilter(InstalledApp app) =>
        filter.Matches(app.Name, app.Publisher, app.Version, app.InstallLocation);

    private static string NotAvailable => Strings.CommonNotAvailable;

    private void RebuildDetailRows(InstalledApp? app)
    {
        detailView.Items.Clear();

        if (app is null) {
            return;
        }

        Theme theme = appConfig.Theme;

        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailName,      Cell(app.Name),                       theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailVersion,   Cell(app.Version),                    theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailPublisher, Cell(app.Publisher),                  theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailScope,     DescribeScope(app.Scope),             theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailInstalled, DescribeInstallDate(app.InstallDate), theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailSize,      DescribeSize(app),                    theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailLocation,  Cell(app.InstallLocation),            theme));
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
        installedAppsView.HeaderText          = string.Format(CultureInfo.CurrentCulture, Strings.AppsHeader, apps.Count);
        installedAppsView.FooterText          = CommonControl.FooterText(
            appConfig.Theme,
            [CommonControl.RefreshHotKey, CommonControl.SortHotKey, .. filter.FooterHotKeys, CommonControl.ScrollHotKey]);
    }

    private static ListSortColumn<InstalledApp>[] SortColumns => [
        ListSortColumn<InstalledApp>.ByText(Strings.ListColumnName, 0, app => app.Name),
        ListSortColumn<InstalledApp>.ByVersion(Strings.ListColumnVersion, 1, app => app.Version),
        ListSortColumn<InstalledApp>.ByText(Strings.ListColumnPublisher, 2, app => app.Publisher),
        ListSortColumn<InstalledApp>.ByText(Strings.ListColumnScope, 3, app => DescribeScope(app.Scope)),
        ListSortColumn<InstalledApp>.ByValue<DateTime>(Strings.AppsColumnInstalled, 4, app => app.InstallDate),
        ListSortColumn<InstalledApp>.ByValue<long>(Strings.ListColumnSize, 5, app => app.EstimatedSizeKb),
        ListSortColumn<InstalledApp>.ByText(Strings.AppsColumnLocation, 6, app => app.InstallLocation)
    ];
}
