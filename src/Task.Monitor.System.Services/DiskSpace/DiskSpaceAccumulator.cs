namespace Task.Monitor.System.Services.DiskSpace;

// Pure in-memory aggregation of a scan in progress - no filesystem access, no interop. Fed a
// stream of (folder, file, size) triples by a walker, it maintains a running byte total per
// scan-root-level folder (not a full recursive tree of every folder at every depth - see the
// note on RootLevelTotals below) and a bounded top-N list of the largest files seen. Kept free
// of I/O so it is unit testable without touching disk; DiskSpaceWalker is what actually calls
// Directory.EnumerateFileSystemEntries.
public sealed class DiskSpaceAccumulator
{
    public const int MaxTrackedFiles = 500;

    private readonly string rootPath;
    private long rootTotalBytes;

    // Only the scan root's immediate children are tracked - not one entry per folder at every
    // depth in the scanned volume. A full recursive tree is O(total folders on disk), which for a
    // whole-drive scan can be hundreds of thousands to millions of live path strings retained for
    // the life of the scan (and, since nothing ever cleared it, well beyond); the treemap only
    // ever draws this one level today, so anything deeper was pure memory cost with no reader.
    // A file several levels down still counts toward its root-level ancestor's total here - it
    // just isn't given its own node in the published tree.
    private readonly Dictionary<string, long> rootLevelTotals = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<DiskSpaceFileEntry> topFiles = new(MaxTrackedFiles + 1);

    // Backs the progress bar: root-level folders are all discovered in one pass (the root's own
    // enumeration, at the very start of the walk), so the total settles almost immediately rather
    // than growing throughout the scan. A folder counts as completed once the walk has moved on to
    // a different root-level folder - safe because DiskSpaceWalker's stack-based depth-first walk
    // always exhausts one whole branch before returning to a sibling.
    private readonly HashSet<string> rootLevelFolders = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> completedRootLevelFolders = new(StringComparer.OrdinalIgnoreCase);
    private string? currentRootLevelFolder;

    private long filesScanned;
    private long foldersScanned;
    private long foldersSkipped;
    private long totalBytesScanned;
    private string currentPath;

    public DiskSpaceAccumulator(string rootPath)
    {
        this.rootPath = rootPath;
        currentPath = rootPath;
    }

    // Called by the walker as soon as a subdirectory is found (queued to visit later), not only
    // once it is actually visited - this is what lets RootLevelFoldersTotal be known up front.
    public void RegisterDiscoveredFolder(string parentFolder, string path)
    {
        if (string.Equals(parentFolder, rootPath, StringComparison.OrdinalIgnoreCase)) {
            rootLevelFolders.Add(path);
        }
    }

    // Called when the walker starts enumerating a directory - tracks the scan-cursor position and
    // ensures a root-level folder with no files of its own still appears in the tree with a zero
    // total (folders below the root level have no node of their own to seed - see RootLevelTotals).
    public void EnterFolder(string path)
    {
        foldersScanned++;
        currentPath = path;

        string? rootLevelFolder = RootLevelAncestorOf(path);

        if (rootLevelFolder is null) {
            return;
        }

        rootLevelTotals.TryAdd(rootLevelFolder, 0);

        if (currentRootLevelFolder is not null &&
            !string.Equals(currentRootLevelFolder, rootLevelFolder, StringComparison.OrdinalIgnoreCase)) {
            completedRootLevelFolders.Add(currentRootLevelFolder);
        }

        currentRootLevelFolder = rootLevelFolder;
    }

    // Called once, after the walk finishes successfully - the depth-first walk never naturally
    // "moves on" from the very last root-level folder it visits, so nothing else marks it done.
    public void MarkScanComplete()
    {
        if (currentRootLevelFolder is not null) {
            completedRootLevelFolders.Add(currentRootLevelFolder);
        }
    }

    // Called when the walker declines to enter a directory (access denied, reparse point loop).
    public void SkipFolder() => foldersSkipped++;

    // Walks up from path to find the immediate child of the scan root that contains it - null for
    // the root itself. Used both to attribute a file's size to the right root-level bucket and to
    // attribute walk progress to a root-level folder.
    private string? RootLevelAncestorOf(string path)
    {
        if (string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        string current = path;

        while (true) {
            string? parent = Path.GetDirectoryName(current);

            if (parent is null) {
                return null;
            }

            if (string.Equals(parent, rootPath, StringComparison.OrdinalIgnoreCase)) {
                return current;
            }

            current = parent;
        }
    }

    public void AddFile(string parentFolder, string fullPath, long sizeBytes)
    {
        filesScanned++;
        totalBytesScanned += sizeBytes;
        rootTotalBytes += sizeBytes;

        string? rootLevelFolder = RootLevelAncestorOf(parentFolder);

        if (rootLevelFolder is not null) {
            rootLevelTotals[rootLevelFolder] = rootLevelTotals.GetValueOrDefault(rootLevelFolder) + sizeBytes;
        }

        TrackTopFile(fullPath, sizeBytes);
    }

    // Keeps a sorted top-N rather than sorting the full file list on every snapshot, since a
    // snapshot can be taken many times a second during a fast scan of a large tree.
    private void TrackTopFile(string fullPath, long sizeBytes)
    {
        int insertAt = topFiles.FindIndex(entry => sizeBytes > entry.SizeBytes);

        if (insertAt < 0) {
            if (topFiles.Count < MaxTrackedFiles) {
                topFiles.Add(new DiskSpaceFileEntry { FullPath = fullPath, SizeBytes = sizeBytes });
            }

            return;
        }

        topFiles.Insert(insertAt, new DiskSpaceFileEntry { FullPath = fullPath, SizeBytes = sizeBytes });

        if (topFiles.Count > MaxTrackedFiles) {
            topFiles.RemoveAt(topFiles.Count - 1);
        }
    }

    public DiskSpaceSpecs Snapshot(DiskSpaceScanState state, long elapsedMilliseconds, string? errorMessage = null) =>
        new() {
            RootPath = rootPath,
            State = state,
            ElapsedMilliseconds = elapsedMilliseconds,
            FilesScanned = filesScanned,
            FoldersScanned = foldersScanned,
            FoldersSkipped = foldersSkipped,
            TotalBytesScanned = totalBytesScanned,
            CurrentPath = state == DiskSpaceScanState.Scanning ? currentPath : string.Empty,
            RootLevelFoldersTotal = rootLevelFolders.Count,
            RootLevelFoldersCompleted = completedRootLevelFolders.Count,
            RootNode = BuildRootNode(),
            TopFiles = [.. topFiles],
            ErrorMessage = errorMessage
        };

    // Builds just the scan root and its immediate children from the bounded root-level totals -
    // O(top-level folder count) regardless of how many folders exist deeper in the scanned volume,
    // unlike a full recursive rebuild of every folder at every depth on every throttled publish.
    private DiskSpaceFolderNode BuildRootNode()
    {
        List<DiskSpaceFolderNode> children = [.. rootLevelTotals
            .Select(pair => new DiskSpaceFolderNode {
                Path = pair.Key,
                Name = NameOf(pair.Key),
                TotalBytes = pair.Value
            })
            .OrderByDescending(node => node.TotalBytes)];

        return new DiskSpaceFolderNode {
            Path = rootPath,
            Name = NameOf(rootPath),
            TotalBytes = rootTotalBytes,
            Children = children
        };
    }

    private static string NameOf(string path)
    {
        string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? path : name;
    }
}
