#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Power.Providers.MacOS;

namespace Task.Monitor.System.Services.Tests.Power;

public sealed class SmcTests
{
    [Fact]
    public void DecodeKey_Reverses_EncodeKey()
    {
        Assert.Equal("Tp0K", Smc.DecodeKey(Smc.EncodeKey("Tp0K")));
    }

    [Theory]
    [InlineData("flt ", new byte[] { 0x00, 0x00, 0x2A, 0x42 }, 42.5)]
    [InlineData("sp78", new byte[] { 0x2D, 0x80 },             45.5)]
    [InlineData("sp78", new byte[] { 0xFF, 0x00 },             -1.0)]
    [InlineData("fpe2", new byte[] { 0x1F, 0x40 },             2000.0)]
    [InlineData("ui8 ", new byte[] { 0x02 },                   2.0)]
    [InlineData("ui16", new byte[] { 0x01, 0x2C },             300.0)]
    [InlineData("ui32", new byte[] { 0x00, 0x00, 0x08, 0x74 }, 2164.0)]
    public void Decode_Reads_Each_Data_Type(string dataType, byte[] bytes, double expected)
    {
        Assert.Equal(expected, Smc.Decode(dataType, bytes));
    }

    [Theory]
    [InlineData("hex_", new byte[] { 0x01 })]
    [InlineData("flt ", new byte[] { 0x01, 0x02 })]
    public void Decode_Returns_Null_For_Unknown_Or_Short_Data(string dataType, byte[] bytes)
    {
        Assert.Null(Smc.Decode(dataType, bytes));
    }

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
    public void ReadKeys_Lists_The_Temperature_Keys()
    {
        using Smc? smc = Smc.Open();

        Skip.If(smc is null, "AppleSMC is not available");

        List<string> keys = smc!.ReadKeys();

        Assert.Contains("#KEY", keys);
        Assert.Contains(keys, key => key.StartsWith('T'));
    }

    [SkippableFact]
    public void ReadValue_Reads_The_System_Total_Power()
    {
        using Smc? smc = Smc.Open();

        Skip.If(smc is null, "AppleSMC is not available");

        Assert.InRange(smc!.ReadValue("PSTR") ?? -1, 0.1, 1000);
        Assert.Null(smc.ReadValue("ZZZZ"));
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
