using Task.Monitor.System.Services.Thermal;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Services.Tests.Thermal;

public sealed partial class ThermalServiceTests
{
    private sealed class FakeProvider : IThermalProvider
    {
        public void Dispose() { }

        public string Name => "Fake";

        public IEnumerable<ThermalSensor> Read() => [];

        public bool TryInitialise() => true;
    }

    [Fact]
    public void LogProviders_Gives_A_Working_Providers_Sensor_Count()
    {
        string       name     = $"Probe {Guid.NewGuid()}";
        FakeProvider provider = new();

        using TraceCapture capture = new();

        new ThermalService().LogProviders([new(name, "OK", provider)], new() { [provider] = 3 });

        Assert.Equal([$"ThermalService: {name} OK, 3 sensors"], capture.LinesContaining(name));
    }

    // Providers are probed again every few seconds. Only a different outcome is worth a line.
    [Fact]
    public void LogProviders_Writes_Again_Only_When_A_Result_Changes()
    {
        string         name    = $"Probe {Guid.NewGuid()}";
        ThermalService service = new();

        using TraceCapture capture = new();

        service.LogProviders([new(name, "NOT FOUND, not installed", null)], new());
        service.LogProviders([new(name, "NOT FOUND, not installed", null)], new());
        service.LogProviders([new(name, "FAILED, access denied", null)], new());

        Assert.Equal(
            [
                $"ThermalService: {name} NOT FOUND, not installed",
                $"ThermalService: {name} FAILED, access denied"
            ],
            capture.LinesContaining(name));

        Assert.Contains("ThermalService: providers changed", capture.Lines);
    }
}
