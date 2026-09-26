namespace Task.Monitor.System.Services.DiskSpace;

public sealed class DiskSpaceFolderNode
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public long TotalBytes { get; init; }
    public IReadOnlyList<DiskSpaceFolderNode> Children { get; init; } = [];
}
