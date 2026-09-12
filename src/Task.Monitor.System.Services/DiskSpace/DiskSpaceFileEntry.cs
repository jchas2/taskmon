namespace Task.Monitor.System.Services.DiskSpace;

public sealed class DiskSpaceFileEntry
{
    public required string FullPath { get; init; }

    public long SizeBytes { get; init; }
}
