namespace Task.Monitor.System.Services.DiskSpace;

public static class DiskSpaceWalker
{
    public static void Walk(
        string rootPath,
        DiskSpaceAccumulator accumulator,
        CancellationToken cancellationToken,
        Action? onFolderVisited = null)
    {
        Stack<string> pending = new();
        pending.Push(rootPath);

        while (pending.Count > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pending.Pop();
            
            VisitDirectory(
                directory, 
                accumulator, 
                pending, 
                cancellationToken);
            
            onFolderVisited?.Invoke();
        }
    }

    private static void VisitDirectory(
        string directory,
        DiskSpaceAccumulator accumulator,
        Stack<string> pending,
        CancellationToken cancellationToken)
    {
        accumulator.EnterFolder(directory);

        IEnumerable<FileSystemInfo> entries;

        try {
            // TODO: Use native calls.
            entries = new DirectoryInfo(directory).EnumerateFileSystemInfos();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
            accumulator.SkipFolder();
            return;
        }

        try {
            foreach (FileSystemInfo entry in entries) {
                cancellationToken.ThrowIfCancellationRequested();
                VisitEntry(directory, entry, accumulator, pending);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
            accumulator.SkipFolder();
        }
    }

    private static void VisitEntry(
        string directory, 
        FileSystemInfo entry, 
        DiskSpaceAccumulator accumulator, 
        Stack<string> pending)
    {
        FileAttributes attributes;

        try {
            attributes = entry.Attributes;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
            return;
        }

        // Junctions and symlinks are skipped.
        if (attributes.HasFlag(FileAttributes.ReparsePoint)) {
            return;
        }

        if (entry is DirectoryInfo) {
            accumulator.RegisterDiscoveredFolder(directory, entry.FullName);
            pending.Push(entry.FullName);
            return;
        }

        try {
            accumulator.AddFile(directory, entry.FullName, ((FileInfo)entry).Length);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
        }
    }
}
