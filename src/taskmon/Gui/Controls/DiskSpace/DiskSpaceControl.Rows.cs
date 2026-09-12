using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.Gui.Controls.DiskSpace;

public sealed partial class DiskSpaceControl
{
    // Diffs against the rows already on screen by full path, the same way ProcessControl updates
    // its 500+ live process rows, instead of clearing and rebuilding up to 500 rows from scratch
    // on every throttled publish. A file's displayed text never changes once it has a row (each
    // file is walked exactly once, at a fixed size), so a tick where the top-N membership and
    // order haven't shifted costs no new row objects at all - only ticks that actually add, drop,
    // or reorder entries do any allocation, and only for the entries that changed.
    private void RebuildFileRows(IReadOnlyList<DiskSpaceFileEntry> files)
    {
        if (files.Count == 0) {
            filesView.Items.Clear();
        }
        else {
            HashSet<string> currentPaths = new(files.Count, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Count; i++) {
                currentPaths.Add(files[i].FullPath);
            }

            for (int i = filesView.Items.Count - 1; i >= 0; i--) {
                FileListViewItem item = (FileListViewItem)filesView.Items[i];

                if (!currentPaths.Contains(item.FullPath)) {
                    filesView.Items.RemoveAt(i);
                }
            }

            Dictionary<string, FileListViewItem> rowsByPath = filesView.Items
                .Cast<FileListViewItem>()
                .ToDictionary(row => row.FullPath, StringComparer.OrdinalIgnoreCase);

            Color background = appConfig.DefaultTheme.Background;
            Color foreground = appConfig.DefaultTheme.Foreground;

            for (int i = 0; i < files.Count; i++) {
                if (rowsByPath.TryGetValue(files[i].FullPath, out FileListViewItem? existing)) {
                    int insertAt = Math.Min(i, filesView.Items.Count - 1);
                    filesView.Items.Remove(existing);
                    filesView.Items.InsertAt(insertAt, existing);
                }
                else {
                    filesView.Items.InsertAt(i, new FileListViewItem(files[i], background, foreground));
                }
            }
        }

        // The scan-control hints (s Start scanning / c Cancel) live on the heat map's own border
        // now, not repeated here - this footer is just about the list itself.
        filesView.FooterText =
            $"{files.Count} of top {DiskSpaceAccumulator.MaxTrackedFiles} largest files     " +
            "↑ ↓ PgUp PgDn Scroll";
    }

    private sealed class FileListViewItem : ListViewItem
    {
        public FileListViewItem(DiskSpaceFileEntry file, Color background, Color foreground)
            : base(Path.GetFileName(file.FullPath), background, foreground)
        {
            FullPath = file.FullPath;

            SubItems.AddRange(
                new ListViewSubItem(this, file.SizeBytes.ToFormattedByteSize(), background, foreground),
                new ListViewSubItem(this, Path.GetDirectoryName(file.FullPath) ?? file.FullPath, background, foreground));
        }

        public string FullPath { get; }
    }
}
