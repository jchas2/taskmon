using Task.Monitor.System.Services.Health;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class HealthServiceTrackedVolumesTests
{
    [Fact]
    public void A_Read_Only_System_Volume_Is_Tracked() =>
        Assert.True(HealthService.IsTracked(isSystem: true, isReadOnly: true));

    [Fact]
    public void A_Read_Only_Volume_Is_Left_Out() =>
        Assert.False(HealthService.IsTracked(isSystem: false, isReadOnly: true));

    [Fact]
    public void A_Writable_Volume_Is_Tracked() =>
        Assert.True(HealthService.IsTracked(isSystem: false, isReadOnly: false));
}
