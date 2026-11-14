using System.Drawing;
using System.Globalization;
using Task.Monitor.Configuration;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.Gui.Controls.Startup;

public sealed partial class StartupControl
{
    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;

    private static string DescribeScope(StartupEntryScope scope) =>
        scope == StartupEntryScope.Machine 
            ? Strings.ScopeMachine
            : Strings.ScopeUser;

    private static string DescribeState(StartupEntryState state) => state switch {
        StartupEntryState.Enabled  => Strings.StartupStateEnabled,
        StartupEntryState.Disabled => Strings.StartupStateDisabled,
                                 _ => Strings.CommonUnknown
    };

    private static string DescribeType(StartupEntrySource source) => source switch {
#if __WIN32__
        StartupEntrySource.RunKey        => Strings.StartupTypeRun,
        StartupEntrySource.RunOnceKey    => Strings.StartupTypeRunOnce,
        StartupEntrySource.StartupFolder => Strings.StartupTypeStartupFolder,
        StartupEntrySource.ScheduledTask => Strings.StartupTypeScheduledTask,
#endif
#if __APPLE__
        StartupEntrySource.OpenAtLogin  => Strings.StartupTypeOpenAtLogin,
        StartupEntrySource.LoginHelper  => Strings.StartupTypeLoginHelper,
        StartupEntrySource.LaunchAgent  => Strings.StartupTypeLaunchAgent,
        StartupEntrySource.LaunchDaemon => Strings.StartupTypeLaunchDaemon,
#endif
                                      _ => source.ToString()
    };

    private static string EntryKey(StartupEntry entry) =>
        $"{(int)entry.Source}|{(int)entry.Scope}|{entry.Origin}|{entry.Name}";

    private bool MatchesFilter(StartupEntry entry) =>
        filter.Matches(
            entry.Name, entry.Publisher, entry.Command, entry.Origin, DescribeType(entry.Source), DescribeScope(entry.Scope));

    private static string NotAvailable => Strings.CommonNotAvailable;

    private void RebuildDetailRows(StartupEntry? entry)
    {
        detailView.Items.Clear();

        if (entry is null) {
            return;
        }

        Theme theme = appConfig.Theme;

        Color statusForeground = entry.State == StartupEntryState.Disabled
            ? theme.RangeMidBackground
            : theme.PropertyValue;

        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailName,      Cell(entry.Name),           theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailPublisher, Cell(entry.Publisher),      theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailType,      DescribeType(entry.Source), theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailScope,     DescribeScope(entry.Scope), theme));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailStatus,    DescribeState(entry.State), theme, statusForeground));
        detailView.Items.Add(CommonControl.PropertyRow(Strings.DetailCommand,   Cell(entry.Command),        theme));
    }

    private void RebuildRows(IReadOnlyList<StartupEntry> entries, IReadOnlyList<string> notes)
    {
        int     topIndex      = startupView.TopIndex;
        int     selectedIndex = startupView.SelectedIndex;
        string? selectedKey   = selectedIndex >= 0 && selectedIndex < visibleEntries.Count
            ? EntryKey(visibleEntries[selectedIndex])
            : null;

        startupView.Items.Clear();

        visibleEntries = sorter.Sort(entries.Where(MatchesFilter));

        Color background         = appConfig.Theme.ListViewBackground;
        Color foreground         = appConfig.Theme.ListViewForeground;
        Color disabledForeground = appConfig.Theme.RangeMidBackground;

        int enabled = entries.Count(entry => entry.State != StartupEntryState.Disabled);

        foreach (StartupEntry entry in visibleEntries) {
            Color statusForeground =
                entry.State == StartupEntryState.Disabled ? disabledForeground : foreground;

            ListViewItem row = new(new[] {
                new ListViewSubItem(null!, Cell(entry.Name),           background, foreground),
                new ListViewSubItem(null!, Cell(entry.Publisher),      background, foreground),
                new ListViewSubItem(null!, DescribeType(entry.Source), background, foreground),
                new ListViewSubItem(null!, DescribeScope(entry.Scope), background, foreground),
                new ListViewSubItem(null!, DescribeState(entry.State), background, statusForeground),
                new ListViewSubItem(null!, Cell(entry.Command),        background, foreground)
            }) {
                Checked = entry.State == StartupEntryState.Enabled
            };

            startupView.Items.Add(row);
        }

        SelectFollowedEntry(selectedKey, selectedIndex, topIndex);

        string headerNotes = notes.Count > 0 ? $"   {string.Join("  ", notes)}" : string.Empty;

        startupView.EmptyListViewText   = filter.IsActive ? ListFilter.NoMatchesText : GatheringText;
        startupView.HeaderHighlightText = filter.BuildHeaderText(visibleEntries.Count, entries.Count);
        startupView.HeaderText          = string.Format(CultureInfo.CurrentCulture, Strings.StartupHeader, enabled, entries.Count) + headerNotes;
        startupView.FooterText          = CommonControl.FooterText(
            appConfig.Theme,
            [
                .. CanSetEnabled ? [ToggleHotKey] : Array.Empty<(string, string)>(),
                CommonControl.RefreshHotKey,
                CommonControl.SortHotKey,
                .. filter.FooterHotKeys,
                CommonControl.ScrollHotKey
            ]);
    }

    private void SelectFollowedEntry(string? key, int previousIndex, int topIndex)
    {
        if (visibleEntries.Count == 0) {
            return;
        }

        int index = -1;

        if (key is not null) {
            for (int i = 0; i < visibleEntries.Count; i++) {
                if (EntryKey(visibleEntries[i]) == key) {
                    index = i;
                    break;
                }
            }
        }

        if (index < 0) {
            index = Math.Clamp(previousIndex, 0, visibleEntries.Count - 1);
        }

        startupView.TopIndex      = topIndex;
        startupView.SelectedIndex = index;
        
        startupView.ScrollSelectedIntoView();
    }

    private static ListSortColumn<StartupEntry>[] SortColumns => [
        ListSortColumn<StartupEntry>.ByText(Strings.ListColumnName, 0, entry => entry.Name),
        ListSortColumn<StartupEntry>.ByText(Strings.ListColumnPublisher, 1, entry => entry.Publisher),
        ListSortColumn<StartupEntry>.ByValue<StartupEntrySource>(Strings.StartupColumnType, 2, entry => entry.Source),
        ListSortColumn<StartupEntry>.ByText(Strings.ListColumnScope, 3, entry => DescribeScope(entry.Scope)),
        ListSortColumn<StartupEntry>.ByText(Strings.ListColumnStatus, 4, entry => DescribeState(entry.State)),
        ListSortColumn<StartupEntry>.ByText(Strings.StartupColumnCommand, 5, entry => entry.Command)
    ];
}   
