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

        IEnumerable<string> entries;

        try {
            entries = Directory.EnumerateFileSystemEntries(directory);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
            accumulator.SkipFolder();
            return;
        }

        try {
            foreach (string entry in entries) {
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
        string directory, string path, DiskSpaceAccumulator accumulator, Stack<string> pending)
    {
        FileAttributes attributes;

        try {
            attributes = File.GetAttributes(path);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
            return;
        }

        // Junctions and symlinks are skipped rather than followed, to avoid an infinite loop from
        // a directory that links back to one of its own ancestors.
        if (attributes.HasFlag(FileAttributes.ReparsePoint)) {
            return;
        }

        if (attributes.HasFlag(FileAttributes.Directory)) {
            accumulator.RegisterDiscoveredFolder(directory, path);
            pending.Push(path);
            return;
        }

        try {
            accumulator.AddFile(directory, path, new FileInfo(path).Length);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
        }
    }
}
