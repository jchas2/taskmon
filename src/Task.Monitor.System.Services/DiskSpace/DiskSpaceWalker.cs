namespace Task.Monitor.System.Services.DiskSpace;

// Cancellable depth-first walk of a directory tree, feeding sizes into a DiskSpaceAccumulator.
// Uses an explicit stack rather than recursion so a very deep tree can't blow the call stack, and
// checks cancellation between every directory and every entry within it rather than only at the
// top level. Free of any WorkerService/Publish concerns - `onFolderVisited` lets the caller decide
// how and when to react to progress (e.g. throttled publishing) - so this stays testable against a
// real temp directory tree with no service or timing involved.
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
            VisitDirectory(directory, accumulator, pending, cancellationToken);
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
            // Enumerating FileSystemInfo (rather than plain path strings via
            // EnumerateFileSystemEntries) captures name, attributes, and size from the same
            // per-entry native call the enumeration already makes - on Windows that's a single
            // WIN32_FIND_DATA per FindNextFile result; on Unix, FileSystemInfo lazily stats an
            // entry on first property access and caches the result for the rest of that
            // instance's lifetime. Either way it's one native call per entry, not the three
            // (enumerate, then a separate GetAttributes, then a separate FileInfo.Length) the
            // previous string-based version paid for every single file.
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
            // Enumeration is lazy: a permission error can surface mid-iteration rather than on
            // the call above. Whatever was seen before the failure is still counted.
            accumulator.SkipFolder();
        }
    }

    private static void VisitEntry(
        string directory, FileSystemInfo entry, DiskSpaceAccumulator accumulator, Stack<string> pending)
    {
        FileAttributes attributes;

        try {
            attributes = entry.Attributes;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
            return;
        }

        // Junctions and symlinks are skipped rather than followed, to avoid an infinite loop from
        // a directory that links back to one of its own ancestors.
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
