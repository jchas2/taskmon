namespace Task.Monitor.System.Services.DiskSpace;

// An immutable snapshot of one folder's accumulated size at the moment it was published - taken
// while a scan may still be running, so TotalBytes only reflects what has been found so far.
public sealed class DiskSpaceFolderNode
{
    public required string Path { get; init; }

    public required string Name { get; init; }

    public long TotalBytes { get; init; }

    public IReadOnlyList<DiskSpaceFolderNode> Children { get; init; } = [];
}
