namespace Task.Monitor.System.Services.DiskSpace;

public static class ScanRootProvider
{
    public static IReadOnlyList<string> GetCandidates() => GetCandidates(EnumerateRealDrives());

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

        if (drive.RootPath == "/" || drive.RootPath.StartsWith("/Volumes/", StringComparison.Ordinal)) {
            return true;
        }

        return drive.DriveType 
            is DriveType.Fixed 
            or DriveType.Removable 
            or DriveType.Network;
    }
}
