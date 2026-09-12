using System.Drawing;

namespace Task.Monitor.System.Services.DiskSpace;

public readonly struct TreemapCell
{
    public required string Id { get; init; }

    public required Rectangle Bounds { get; init; }
}
