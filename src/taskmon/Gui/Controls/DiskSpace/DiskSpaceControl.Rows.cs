using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.Gui.Controls.DiskSpace;

public sealed partial class DiskSpaceControl
{
    private void RebuildFileRows(IReadOnlyList<DiskSpaceFileEntry> files)
    {
        filesView.Items.Clear();

        Color background = appConfig.DefaultTheme.Background;
        Color foreground = appConfig.DefaultTheme.Foreground;

        foreach (DiskSpaceFileEntry file in files) {
            ListViewItem row = new(new[] {
                new ListViewSubItem(null!, Path.GetFileName(file.FullPath), background, foreground),
                new ListViewSubItem(null!, file.SizeBytes.ToFormattedByteSize(), background, foreground),
                new ListViewSubItem(null!, Path.GetDirectoryName(file.FullPath) ?? file.FullPath, background, foreground)
            });

            filesView.Items.Add(row);
        }

        filesView.FooterText =
            $"{files.Count} of top {DiskSpaceAccumulator.MaxTrackedFiles} largest files     " +
            "s Start scanning   c Cancel   ↑ ↓ PgUp PgDn Scroll";
    }
}
