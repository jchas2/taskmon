namespace Task.Monitor.System.Services.DiskSpace;

public sealed class DiskSpaceAccumulator
{
    public const int MaxTrackedFiles = 500;

    private readonly string rootPath;
    private long rootTotalBytes;

    private readonly Dictionary<string, long> rootLevelTotals = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<DiskSpaceFileEntry> topFiles = new(MaxTrackedFiles + 1);

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

    public void RegisterDiscoveredFolder(string parentFolder, string path)
    {
        if (string.Equals(parentFolder, rootPath, StringComparison.OrdinalIgnoreCase)) {
            rootLevelFolders.Add(path);
        }
    }

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

    public void MarkScanComplete()
    {
        if (currentRootLevelFolder is not null) {
            completedRootLevelFolders.Add(currentRootLevelFolder);
        }
    }

    public void SkipFolder() => foldersSkipped++;

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

    private void TrackTopFile(string fullPath, long sizeBytes)
    {
        int insertAt = topFiles.FindIndex(entry => sizeBytes > entry.SizeBytes);

        if (insertAt < 0) {
            if (topFiles.Count < MaxTrackedFiles) {
                topFiles.Add(new DiskSpaceFileEntry {
                    FullPath = fullPath, 
                    SizeBytes = sizeBytes
                });
            }

            return;
        }

        topFiles.Insert(insertAt, new DiskSpaceFileEntry {
            FullPath = fullPath, 
            SizeBytes = sizeBytes
        });

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
