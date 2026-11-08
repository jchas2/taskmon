using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.Gui.Controls.Startup;

public sealed partial class StartupControl
{
    private const string NotAvailable = "N/A";

    private static readonly ListSortColumn<StartupEntry>[] SortColumns = [
        ListSortColumn<StartupEntry>.ByText("NAME", 0, entry => entry.Name),
        ListSortColumn<StartupEntry>.ByText("PUBLISHER", 1, entry => entry.Publisher),
        ListSortColumn<StartupEntry>.ByValue<StartupEntrySource>("TYPE", 2, entry => entry.Source),
        ListSortColumn<StartupEntry>.ByText("SCOPE", 3, entry => DescribeScope(entry.Scope)),
        ListSortColumn<StartupEntry>.ByText("STATUS", 4, entry => DescribeState(entry.State)),
        ListSortColumn<StartupEntry>.ByText("COMMAND", 5, entry => entry.Command)
    ];

    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;

    private static string DescribeScope(StartupEntryScope scope) =>
        scope == StartupEntryScope.Machine 
            ? "Machine" 
            : "User";

    private static string DescribeState(StartupEntryState state) => state switch {
        StartupEntryState.Enabled  => "Enabled",
        StartupEntryState.Disabled => "Disabled",
                                 _ => "Unknown"
    };

    private static string DescribeType(StartupEntrySource source) => source switch {
#if __WIN32__
        StartupEntrySource.RunKey        => "Run",
        StartupEntrySource.RunOnceKey    => "RunOnce",
        StartupEntrySource.StartupFolder => "Startup Folder",
        StartupEntrySource.ScheduledTask => "Scheduled Task",
#endif
#if __APPLE__
        StartupEntrySource.OpenAtLogin  => "Open at Login",
        StartupEntrySource.LoginHelper  => "Login Helper",
        StartupEntrySource.LaunchAgent  => "Launch Agent",
        StartupEntrySource.LaunchDaemon => "Launch Daemon",
#endif
                                      _ => source.ToString()
    };

    private static string EntryKey(StartupEntry entry) =>
        $"{(int)entry.Source}|{(int)entry.Scope}|{entry.Origin}|{entry.Name}";

    private bool MatchesFilter(StartupEntry entry) =>
        filter.Matches(
            entry.Name, entry.Publisher, entry.Command, entry.Origin, DescribeType(entry.Source), DescribeScope(entry.Scope));

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

        detailView.Items.Add(CommonControl.PropertyRow("Name",      Cell(entry.Name),           theme));
        detailView.Items.Add(CommonControl.PropertyRow("Publisher", Cell(entry.Publisher),      theme));
        detailView.Items.Add(CommonControl.PropertyRow("Type",      DescribeType(entry.Source), theme));
        detailView.Items.Add(CommonControl.PropertyRow("Scope",     DescribeScope(entry.Scope), theme));
        detailView.Items.Add(CommonControl.PropertyRow("Status",    DescribeState(entry.State), theme, statusForeground));
        detailView.Items.Add(CommonControl.PropertyRow("Command",   Cell(entry.Command),        theme));
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
        startupView.HeaderText          = $"{enabled} enabled / {entries.Count} total{headerNotes}";
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
}   
