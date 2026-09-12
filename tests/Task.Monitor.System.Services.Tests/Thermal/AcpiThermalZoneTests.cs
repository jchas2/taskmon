using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Tests.Thermal;

public sealed class AcpiThermalZoneTests
{
    [Fact]
    public void Converts_Tenths_Of_Kelvin_To_Celsius()
    {
        // 3132 tenth-K = 313.2 K = 40.05 C
        Assert.Equal(40.05, AcpiThermalZone.TenthKelvinToCelsius(3132)!.Value, precision: 2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(2000)]   // ~ -73 C, implausible
    public void Rejects_Non_Positive_Or_Implausible_Values(double tenthKelvin)
    {
        Assert.Null(AcpiThermalZone.TenthKelvinToCelsius(tenthKelvin));
    }

    [Theory]
    [InlineData(@"ACPI\ThermalZone\CPUZ_0", ThermalComponent.Cpu)]
    [InlineData(@"\_TZ.TZ00", ThermalComponent.Other)]
    [InlineData(null, ThermalComponent.Other)]
    public void Classifies_Zones_By_Name(string? instanceName, ThermalComponent expected)
    {
        Assert.Equal(expected, AcpiThermalZone.ClassifyZone(instanceName));
    }

    [Theory]
    [InlineData(@"ACPI\ThermalZone\TZ00_0", "TZ00_0")]
    [InlineData(@"\_TZ.CPUZ", "CPUZ")]
    [InlineData("", "Thermal Zone")]
    public void Derives_A_Short_Label(string instanceName, string expected)
    {
        Assert.Equal(expected, AcpiThermalZone.ZoneLabel(instanceName));
    }
}
