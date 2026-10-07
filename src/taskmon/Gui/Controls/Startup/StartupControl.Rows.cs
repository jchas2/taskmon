using System.Drawing;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.Gui.Controls.Startup;

public sealed partial class StartupControl
{
    private const string NotAvailable = "N/A";

    private void RebuildRows(IReadOnlyList<StartupEntry> entries, IReadOnlyList<string> notes)
    {
        startupView.Items.Clear();

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;
        Color disabledForeground = appConfig.Theme.RangeMidForeground;

        int enabled = 0;

        foreach (StartupEntry entry in entries) {
            if (entry.State != StartupEntryState.Disabled) {
                enabled++;
            }

            Color statusForeground =
                entry.State == StartupEntryState.Disabled ? disabledForeground : foreground;

            ListViewItem row = new(new[] {
                new ListViewSubItem(null!, Cell(entry.Name), background, foreground),
                new ListViewSubItem(null!, Cell(entry.Publisher), background, foreground),
                new ListViewSubItem(null!, DescribeType(entry.Source), background, foreground),
                new ListViewSubItem(null!, DescribeScope(entry.Scope), background, foreground),
                new ListViewSubItem(null!, DescribeState(entry.State), background, statusForeground),
                new ListViewSubItem(null!, Cell(entry.Command), background, foreground)
            });

            startupView.Items.Add(row);
        }

        string footerNotes = notes.Count > 0 ? $"     {string.Join("  ", notes)}" : string.Empty;

        startupView.FooterText =
            $"{enabled} enabled / {entries.Count} total     r Refresh     ↑ ↓ PgUp PgDn Scroll{footerNotes}";
    }

    private void RebuildDetailRows(StartupEntry? entry)
    {
        detailView.Items.Clear();

        if (entry is null) {
            return;
        }

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;
        Color disabledForeground = appConfig.Theme.RangeMidForeground;

        Color statusForeground =
            entry.State == StartupEntryState.Disabled ? disabledForeground : foreground;

        detailView.Items.Add(new ListViewItem(["Name", Cell(entry.Name)], background, foreground));
        detailView.Items.Add(new ListViewItem(["Publisher", Cell(entry.Publisher)], background, foreground));
        detailView.Items.Add(new ListViewItem(["Type", DescribeType(entry.Source)], background, foreground));
        detailView.Items.Add(new ListViewItem(["Scope", DescribeScope(entry.Scope)], background, foreground));

        detailView.Items.Add(new ListViewItem(
            [new ListViewSubItem(null!, "Status", background, foreground),
             new ListViewSubItem(null!, DescribeState(entry.State), background, statusForeground)]));

        detailView.Items.Add(new ListViewItem(["Command", Cell(entry.Command)], background, foreground));
    }

    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotAvailable : value;

    private static string DescribeType(StartupEntrySource source) => source switch {
#if __WIN32__
        StartupEntrySource.RunKey => "Run",
        StartupEntrySource.RunOnceKey => "RunOnce",
        StartupEntrySource.StartupFolder => "Startup Folder",
        StartupEntrySource.ScheduledTask => "Scheduled Task",
#endif
#if __APPLE__
        StartupEntrySource.OpenAtLogin => "Open at Login",
        StartupEntrySource.LoginHelper => "Login Helper",
        StartupEntrySource.LaunchAgent => "Launch Agent",
        StartupEntrySource.LaunchDaemon => "Launch Daemon",
#endif
        _ => source.ToString()
    };

    private static string DescribeScope(StartupEntryScope scope) =>
        scope == StartupEntryScope.Machine ? "Machine" : "User";

    private static string DescribeState(StartupEntryState state) => state switch {
        StartupEntryState.Enabled => "Enabled",
        StartupEntryState.Disabled => "Disabled",
        _ => "Unknown"
    };
}
