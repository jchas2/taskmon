#if __APPLE__
using Task.Monitor.System.Services.Power.Providers.MacOS;

namespace Task.Monitor.System.Services.Tests.Power;

public sealed class SmcPowerProviderTests
{
    [SkippableFact]
    public void TryInitialise_Leaves_Laptops_To_The_Battery_Provider()
    {
        Skip.IfNot(SmartBatteryPowerProvider.IsPresent(), "This Mac has no battery");

        using SmcPowerProvider provider = new();

        Assert.False(provider.TryInitialise());
    }
}
#endif
