#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Power.Providers.MacOS;

namespace Task.Monitor.System.Services.Tests.Power;

public sealed class SmcTests
{
    [Fact]
    public void EncodeKey_Packs_Four_Characters_Big_Endian()
    {
        Assert.Equal(0x50535452u, Smc.EncodeKey("PSTR"));
    }

    [Theory]
    [InlineData("PST")]
    [InlineData("PSTRX")]
    public void EncodeKey_Rejects_A_Key_That_Is_Not_Four_Characters(string key)
    {
        Assert.Throws<ArgumentException>(() => Smc.EncodeKey(key));
    }

    [SkippableFact]
    public void ReadFloat_Reads_The_System_Total_Power()
    {
        using Smc? smc = Smc.Open();

        Skip.If(smc is null, "AppleSMC is not available");

        Assert.InRange(smc!.ReadFloat("PSTR") ?? -1, 0.1, 1000);
        Assert.Null(smc.ReadFloat("ZZZZ"));
    }

    [SkippableFact]
    public void SmcPowerProvider_Leaves_Laptops_To_The_Battery_Provider()
    {
        Skip.IfNot(SmartBatteryPowerProvider.IsPresent(), "This Mac has no battery");

        using SmcPowerProvider provider = new();

        Assert.False(provider.TryInitialise());
    }
}
#endif
