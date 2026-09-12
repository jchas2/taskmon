namespace Task.Monitor.System.Services.DiskSpace;

public sealed class DiskSpaceSpecs
{
    public string RootPath { get; init; } = string.Empty;

    public DiskSpaceScanState State { get; init; } = DiskSpaceScanState.Idle;

    public long ElapsedMilliseconds { get; init; }

    public long FilesScanned { get; init; }

    public long FoldersScanned { get; init; }

    public long FoldersSkipped { get; init; }

    public long TotalBytesScanned { get; init; }

    // The directory the walker is currently enumerating - drives the heat map's scan-cursor
    // highlight. Empty once the scan is not running.
    public string CurrentPath { get; init; } = string.Empty;

    // Backs the progress bar. Total settles almost immediately (the root's own contents are
    // enumerated in one pass at the very start of the walk) rather than growing throughout the
    // scan; Completed only increases once a root-level folder's entire subtree has been walked.
    public int RootLevelFoldersTotal { get; init; }

    public int RootLevelFoldersCompleted { get; init; }

    public DiskSpaceFolderNode? RootNode { get; init; }

    // Already sorted by SizeBytes descending and capped to a bounded top-N by the accumulator.
    public IReadOnlyList<DiskSpaceFileEntry> TopFiles { get; init; } = [];

    public string? ErrorMessage { get; init; }
}
