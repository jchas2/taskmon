#if __APPLE__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Mach;
#endif

namespace Task.Monitor.System.Services.InstalledApps;

public sealed partial class InstalledAppSizeCache
{
#if __APPLE__
    private const int CancellationCheckInterval = 4096;

    // Fast path is through Spotlight's metadata; otherwise fallback to slow path file size sums.
    private static partial long? MeasureBytes(string path, CancellationToken cancellationToken) =>
        Metadata.GetLogicalSize(path) is { } indexed && indexed >= MinimumPlausibleBytes(path)
            ? indexed
            : SumFileSizes(path, cancellationToken);

    // Spotlight answers for items it hasn't indexed yet with a placeholder size (single byte).
    // A bundle holds at least its own Info.plist, so anything smaller can't be its real total.
    private static long MinimumPlausibleBytes(string path)
    {
        FileInfo infoPlist = new(Path.Combine(path, "Contents", "Info.plist"));

        return infoPlist.Exists 
            ? Math.Max(1, infoPlist.Length) 
            : 1;
    }

    private static unsafe long? SumFileSizes(string path, CancellationToken cancellationToken)
    {
        // Use FTS for a fast walk instead of .net's file enumeration; some bundles have +100,000 files
        // and this blows the GC heap. 
        nint pathUtf8 = Marshal.StringToCoTaskMemUTF8(path);

        try {
            byte** pathArgv = stackalloc byte*[2];
            pathArgv[0] = (byte*)pathUtf8;
            pathArgv[1] = null;

            nint fts = Fts.fts_open(pathArgv, Fts.FTS_NOCHDIR | Fts.FTS_PHYSICAL, nint.Zero);

            if (fts == nint.Zero) {
                return null;
            }

            try {
                long bytes   = 0;
                int  visited = 0;

                for (Fts.FTSENT* entry = Fts.fts_read(fts); entry != null; entry = Fts.fts_read(fts)) {
                    if (++visited % CancellationCheckInterval == 0 && cancellationToken.IsCancellationRequested) {
                        return null;
                    }

                    if (entry->fts_info == Fts.FTS_F && entry->fts_statp != null) {
                        bytes += entry->fts_statp->st_size;
                    }
                }

                return bytes;
            }
            finally {
                Fts.fts_close(fts);
            }
        }
        finally {
            Marshal.FreeCoTaskMem(pathUtf8);
        }
    }
#endif
}
