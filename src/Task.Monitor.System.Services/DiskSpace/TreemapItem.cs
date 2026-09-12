namespace Task.Monitor.System.Services.DiskSpace;

// One rectangle-to-be laid out by SquarifiedTreemapLayout: an opaque Id the caller can match the
// resulting TreemapCell back to, and a non-negative Weight proportional to its desired area.
public readonly struct TreemapItem
{
    public required string Id { get; init; }

    public required double Weight { get; init; }
}
