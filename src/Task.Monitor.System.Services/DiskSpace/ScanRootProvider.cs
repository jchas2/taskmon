namespace Task.Monitor.System.Services.DiskSpace;

// Cross-platform candidate list of "roots you'd sensibly want to scan for disk usage" - backed by
// the BCL's already cross-platform DriveInfo rather than any interop, since DriveInfo.GetDrives()
// already abstracts drive letters (Windows) from mount points (Unix) for us. The boot/system
// volume always sorts first, matching what DiskSpaceControl's prompt already defaulted to before
// this existed (Path.GetPathRoot(Environment.SystemDirectory)).
public static class ScanRootProvider
{
    public static IReadOnlyList<string> GetCandidates() => GetCandidates(EnumerateRealDrives());

    // Pure filter/sort over an already-fetched drive list, so it can be exercised in tests
    // against fake data - DriveInfo itself is sealed with no mockable members, so faking what it
    // would report on a machine other than the one running the test isn't otherwise possible.
    internal static IReadOnlyList<string> GetCandidates(IEnumerable<DriveCandidate> drives)
    {
        string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;

        return [.. drives
            .Where(IsScannable)
            .Select(drive => drive.RootPath)
            .OrderBy(path => !string.Equals(path, systemRoot, StringComparison.OrdinalIgnoreCase))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)];
    }

    private static IEnumerable<DriveCandidate> EnumerateRealDrives()
    {
        foreach (DriveInfo drive in DriveInfo.GetDrives()) {
            yield return new DriveCandidate(drive.RootDirectory.FullName, TryGetIsReady(drive), drive.DriveType);
        }
    }

    // DriveInfo.IsReady can throw for a handful of edge-case mounts (a network share that just
    // dropped, a card reader with no card) - treat those the same as "not ready" rather than
    // letting one bad entry blow up the whole candidate list.
    private static bool TryGetIsReady(DriveInfo drive)
    {
        try {
            return drive.IsReady;
        }
        catch (IOException) {
            return false;
        }
    }

    private static bool IsScannable(DriveCandidate drive)
    {
        if (!drive.IsReady) {
            return false;
        }

        // macOS: the boot volume mounts at "/" and everything else of interest appears under
        // "/Volumes/" - this sidesteps the long tail of pseudo-filesystems (devfs, autofs, etc.)
        // that also show up in DriveInfo.GetDrives() on Unix, without needing a DriveFormat
        // denylist that would need constant upkeep as the OS adds new virtual filesystem types.
        if (drive.RootPath == "/" || drive.RootPath.StartsWith("/Volumes/", StringComparison.Ordinal)) {
            return true;
        }

        return drive.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network;
    }
}
