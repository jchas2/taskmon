#if __APPLE__
using Task.Monitor.System.Services.Health;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class HealthServiceVolumesTests
{
    private const string DataVolume = "/System/Volumes/Data";
    private const string RootVolume = "/";

    [Fact]
    public void A_Path_That_Does_Not_Exist_Is_Not_Read_Only() =>
        Assert.False(HealthService.IsReadOnlyVolume("/no/such/volume"));

    [Fact]
    public void The_Data_Volume_Is_Not_Read_Only() =>
        Assert.False(HealthService.IsReadOnlyVolume(DataVolume));

    [Fact]
    public void The_Sealed_Root_Volume_Is_Read_Only() =>
        Assert.True(HealthService.IsReadOnlyVolume(RootVolume));

    [Fact]
    public void The_Sealed_Root_Volume_Is_Still_Listed_As_A_System_Volume()
    {
        HealthVolume root = Assert.Single(HealthService.ReadVolumes(), volume => volume.Name == RootVolume);

        Assert.True(root.IsSystem);
    }
}
#endif
