using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.System.Services.Tests.DiskSpace;

public sealed class ScanRootProviderTests
{
    [Fact]
    public void GetCandidates_Includes_Fixed_Removable_And_Network_Drives_On_Windows()
    {
        DriveCandidate[] drives = [
            new("C:\\", IsReady: true, DriveType.Fixed),
            new("D:\\", IsReady: true, DriveType.Removable),
            new("E:\\", IsReady: true, DriveType.Network)
        ];

        IReadOnlyList<string> candidates = ScanRootProvider.GetCandidates(drives);

        Assert.Equal(["C:\\", "D:\\", "E:\\"], candidates);
    }

    [Fact]
    public void GetCandidates_Excludes_CdRom_And_Ram_And_Unknown_Drives()
    {
        DriveCandidate[] drives = [
            new("C:\\", IsReady: true, DriveType.Fixed),
            new("F:\\", IsReady: true, DriveType.CDRom),
            new("G:\\", IsReady: true, DriveType.Ram),
            new("H:\\", IsReady: true, DriveType.Unknown),
            new("I:\\", IsReady: true, DriveType.NoRootDirectory)
        ];

        IReadOnlyList<string> candidates = ScanRootProvider.GetCandidates(drives);

        Assert.Equal(["C:\\"], candidates);
    }

    [Fact]
    public void GetCandidates_Excludes_Drives_That_Are_Not_Ready()
    {
        DriveCandidate[] drives = [
            new("C:\\", IsReady: true, DriveType.Fixed),
            new("D:\\", IsReady: false, DriveType.Removable)
        ];

        IReadOnlyList<string> candidates = ScanRootProvider.GetCandidates(drives);

        Assert.Equal(["C:\\"], candidates);
    }

    [Fact]
    public void GetCandidates_Includes_The_Root_Volume_And_Volumes_Mounts_On_MacOS()
    {
        DriveCandidate[] drives = [
            new("/", IsReady: true, DriveType.Fixed),
            new("/Volumes/Backup", IsReady: true, DriveType.Fixed),
            new("/Volumes/External SSD", IsReady: true, DriveType.Removable)
        ];

        IReadOnlyList<string> candidates = ScanRootProvider.GetCandidates(drives);

        Assert.Equal(["/", "/Volumes/Backup", "/Volumes/External SSD"], candidates);
    }

    [Fact]
    public void GetCandidates_Excludes_Unix_Pseudo_Filesystem_Mounts_Outside_Volumes()
    {
        DriveCandidate[] drives = [
            new("/", IsReady: true, DriveType.Fixed),
            new("/dev", IsReady: true, DriveType.Unknown),
            new("/private/var/vm", IsReady: true, DriveType.Unknown)
        ];

        IReadOnlyList<string> candidates = ScanRootProvider.GetCandidates(drives);

        Assert.Equal(["/"], candidates);
    }

    [Fact]
    public void GetCandidates_Sorts_The_System_Root_First_Then_Alphabetically()
    {
        string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;

        Assert.False(string.IsNullOrEmpty(systemRoot));

        DriveCandidate[] drives = [
            new("Z:\\", IsReady: true, DriveType.Fixed),
            new("A:\\", IsReady: true, DriveType.Fixed),
            new(systemRoot, IsReady: true, DriveType.Fixed)
        ];

        IReadOnlyList<string> candidates = ScanRootProvider.GetCandidates(drives);

        Assert.Equal([systemRoot, "A:\\", "Z:\\"], candidates);
    }
}
