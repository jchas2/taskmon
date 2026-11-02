#if __WIN32__
using Task.Monitor.System.Services.Health;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class HealthServiceVolumesTests
{
    private static readonly string SystemVolume = Path.GetPathRoot(Environment.SystemDirectory)!;

    [Fact]
    public void A_Drive_That_Does_Not_Exist_Is_Not_Read_Only() =>
        Assert.False(HealthService.IsReadOnlyVolume(UnusedDrive()));

    [Fact]
    public void The_System_Volume_Is_Listed_As_A_System_Volume()
    {
        HealthVolume volume = Assert.Single(HealthService.ReadVolumes(), volume => volume.Name == SystemVolume);

        Assert.True(volume.IsSystem);
    }

    [Fact]
    public void The_System_Volume_Is_Not_Read_Only() =>
        Assert.False(HealthService.IsReadOnlyVolume(SystemVolume));

    private static string UnusedDrive()
    {
        HashSet<char> used = DriveInfo.GetDrives().Select(drive => drive.Name[0]).ToHashSet();

        return $"{Enumerable.Range('D', 23).Select(letter => (char)letter).First(letter => !used.Contains(letter))}:\\";
    }
}
#endif
