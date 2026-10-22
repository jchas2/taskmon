#if __APPLE__
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Services.Power.Providers.MacOS;

namespace Task.Monitor.System.Services.Tests.Power;

public sealed class SmartBatteryPowerProviderTests
{
    [SkippableFact]
    public void Read_Reports_The_System_Draw_On_A_Mac_With_A_Battery()
    {
        Skip.IfNot(SmartBatteryPowerProvider.IsPresent(), "This Mac has no battery");

        using SmartBatteryPowerProvider provider = new();

        Assert.True(provider.TryInitialise());

        PowerReading reading = Assert.Single(provider.Read());

        Assert.Equal(PowerComponent.System, reading.Component);
        Assert.Contains(reading.Rail, new[] { PowerRails.SystemBattery, PowerRails.SystemAdapter });
        Assert.InRange(reading.Watts, 0.1, 500);
    }

    [Fact]
    public void SystemWatts_Falls_Back_To_The_Battery_Discharge_Rate()
    {
        Assert.Equal(8.35, SmartBatteryPowerProvider.SystemWatts(null, 11_300L, -739L, onAdapter: false)!.Value, precision: 2);
    }

    [Fact]
    public void SystemWatts_Is_Unknown_On_The_Adapter_Without_Telemetry()
    {
        Assert.Null(SmartBatteryPowerProvider.SystemWatts(null, 12_000L, 500L, onAdapter: true));
    }

    [Fact]
    public void SystemWatts_Prefers_The_Telemetry_System_Load()
    {
        Assert.Equal(8.355, SmartBatteryPowerProvider.SystemWatts(Telemetry(8_355), 11_300L, -100L, onAdapter: false));
    }

    [Fact]
    public void SystemWatts_Skips_A_Zero_System_Load()
    {
        Assert.Equal(1.13, SmartBatteryPowerProvider.SystemWatts(Telemetry(0), 11_300L, -100L, onAdapter: false)!.Value, precision: 9);
    }

    private static Dictionary<string, object?> Telemetry(long systemLoad) => new() { ["SystemLoad"] = systemLoad };
}
#endif
