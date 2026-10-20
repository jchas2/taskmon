using System.Diagnostics;

namespace Task.Monitor.System.Services.InstalledApps;

public sealed partial class InstalledAppSizeCache
{
#if !__APPLE__
    // Hidden files count towards a bundle's size, and unreadable folders are skipped, not fatal.
    private static readonly EnumerationOptions WalkOptions = new() {
        AttributesToSkip   = 0,
        IgnoreInaccessible = true
    };
#endif

    private readonly Dictionary<string, (DateTime Stamp, long? SizeKb)> sizes = new(StringComparer.Ordinal);

    public InstalledAppsSpecs ComputePending(
        InstalledAppsSpecs specs,
        TimeSpan           budget,
        CancellationToken  cancellationToken)
    {
        if (!specs.Apps.Exists(app => app.SizePending)) {
            return specs;
        }

        Stopwatch          elapsed = Stopwatch.StartNew();
        List<InstalledApp> apps    = new(specs.Apps.Count);

        foreach (InstalledApp app in specs.Apps) {
            if (app.SizePending &&
                app.InstallLocation is { } path &&
                elapsed.Elapsed < budget &&
                TryMeasure(path, cancellationToken, out long? sizeKb)) {

                apps.Add(WithSize(app, sizeKb));
                continue;
            }

            apps.Add(app);
        }

        return new InstalledAppsSpecs { Apps = apps };
    }

    public static long? ComputeSizeKb(string path, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || !Directory.Exists(path)) {
            return null;
        }

        return MeasureBytes(path, cancellationToken) is { } bytes
            ? (bytes + 1023) / 1024
            : null;
    }

    private static partial long? MeasureBytes(string path, CancellationToken cancellationToken);

#if !__APPLE__
    private static partial long? MeasureBytes(string path, CancellationToken cancellationToken)
    {
        long          bytes   = 0;
        Stack<string> pending = new([path]);

        while (pending.Count > 0) {
            if (cancellationToken.IsCancellationRequested) {
                return null;
            }

            string directory = pending.Pop();

            try {
                foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", WalkOptions)) {
                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                        continue;
                    }

                    if (entry is FileInfo file) {
                        bytes += file.Length;
                    }
                    else {
                        pending.Push(entry.FullName);
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
                // The folder vanished or became unreadable mid-walk; count what could be read.
            }
        }

        return bytes;
    }
#endif

    public bool TryGetCached(string path, out long? sizeKb)
    {
        if (sizes.TryGetValue(path, out (DateTime Stamp, long? SizeKb) cached) &&
            cached.Stamp == InstalledAppBundleEntry.GetStamp(path)) {

            sizeKb = cached.SizeKb;
            return true;
        }

        sizeKb = null;
        return false;
    }

    private bool TryMeasure(string path, CancellationToken cancellationToken, out long? sizeKb)
    {
        if (TryGetCached(path, out sizeKb)) {
            return true;
        }

        DateTime stamp = InstalledAppBundleEntry.GetStamp(path);
        sizeKb = ComputeSizeKb(path, cancellationToken);

        // A cancelled walk is partial: leave the app pending rather than cache a wrong size.
        if (cancellationToken.IsCancellationRequested) {
            return false;
        }

        sizes[path] = (stamp, sizeKb);
        return true;
    }

    private static InstalledApp WithSize(InstalledApp app, long? sizeKb) =>
        new() {
            Name             = app.Name,
            Version          = app.Version,
            Publisher        = app.Publisher,
            InstallDate      = app.InstallDate,
            InstallLocation  = app.InstallLocation,
            EstimatedSizeKb  = sizeKb,
            SizePending      = false,
            UninstallCommand = app.UninstallCommand,
            Scope            = app.Scope,
            Origin           = app.Origin
        };
}
