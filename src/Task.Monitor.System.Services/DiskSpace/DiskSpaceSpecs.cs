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
    public string CurrentPath { get; init; } = string.Empty;
    public int RootLevelFoldersTotal { get; init; }
    public int RootLevelFoldersCompleted { get; init; }
    public DiskSpaceFolderNode? RootNode { get; init; }
    public IReadOnlyList<DiskSpaceFileEntry> TopFiles { get; init; } = [];
    public string? ErrorMessage { get; init; }
}
