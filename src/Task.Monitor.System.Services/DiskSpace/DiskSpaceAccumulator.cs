namespace Task.Monitor.System.Services.DiskSpace;

// Pure in-memory aggregation of a scan in progress - no filesystem access, no interop. Fed a
// stream of (folder, file, size) triples by a walker, it maintains a running per-folder byte
// total (each file's size is added to every ancestor folder up to the scan root) and a bounded
// top-N list of the largest files seen. Kept free of I/O so it is unit testable without touching
// disk; DiskSpaceWalker is what actually calls Directory.EnumerateFileSystemEntries.
public sealed class DiskSpaceAccumulator
{
    public const int MaxTrackedFiles = 500;

    private readonly string rootPath;
    private readonly Dictionary<string, long> folderTotals = new(StringComparer.OrdinalIgnoreCase);
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
        folderTotals[rootPath] = 0;
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
    // ensures a folder with no files of its own still appears in the tree with a zero total.
    public void EnterFolder(string path)
    {
        foldersScanned++;
        currentPath = path;
        folderTotals.TryAdd(path, 0);

        string? rootLevelFolder = RootLevelAncestorOf(path);

        if (rootLevelFolder is null) {
            return;
        }

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
    // the root itself. Used only to attribute progress to a root-level folder, not for the
    // ancestor-total bookkeeping AddToAncestors already does.
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

        AddToAncestors(parentFolder, sizeBytes);
        TrackTopFile(fullPath, sizeBytes);
    }

    private void AddToAncestors(string folder, long sizeBytes)
    {
        string? current = folder;

        while (current is not null) {
            folderTotals[current] = folderTotals.GetValueOrDefault(current) + sizeBytes;

            if (string.Equals(current, rootPath, StringComparison.OrdinalIgnoreCase)) {
                break;
            }

            current = Path.GetDirectoryName(current);
        }
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
            RootNode = BuildTree(),
            TopFiles = [.. topFiles],
            ErrorMessage = errorMessage
        };

    // Builds the full nested folder tree from the flat total-per-path map. Left to the layout
    // algorithm to decide how deep it actually recurses when drawing, based on available screen
    // space, rather than limiting depth here.
    private DiskSpaceFolderNode? BuildTree()
    {
        if (!folderTotals.ContainsKey(rootPath)) {
            return null;
        }

        Dictionary<string, List<string>> childrenByParent = new(StringComparer.OrdinalIgnoreCase);

        foreach (string path in folderTotals.Keys) {
            if (string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            string? parent = Path.GetDirectoryName(path);

            if (parent is null) {
                continue;
            }

            if (!childrenByParent.TryGetValue(parent, out List<string>? siblings)) {
                siblings = [];
                childrenByParent[parent] = siblings;
            }

            siblings.Add(path);
        }

        return BuildNode(rootPath, childrenByParent);
    }

    private DiskSpaceFolderNode BuildNode(string path, Dictionary<string, List<string>> childrenByParent)
    {
        List<DiskSpaceFolderNode> children = childrenByParent.TryGetValue(path, out List<string>? childPaths)
            ? [.. childPaths
                .Select(childPath => BuildNode(childPath, childrenByParent))
                .OrderByDescending(node => node.TotalBytes)]
            : [];

        string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(trimmed);

        return new DiskSpaceFolderNode {
            Path = path,
            Name = string.IsNullOrEmpty(name) ? path : name,
            TotalBytes = folderTotals.GetValueOrDefault(path),
            Children = children
        };
    }
}
