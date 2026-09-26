namespace Task.Monitor.System.Services.DiskSpace;

public readonly struct TreemapItem
{
    public required string Id { get; init; }
    public required double Weight { get; init; }
}
