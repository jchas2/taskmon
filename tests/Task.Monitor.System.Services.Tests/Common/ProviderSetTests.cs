using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Services.Tests.Common;

public sealed class ProviderSetTests
{
    private sealed class FakeProvider(string name, bool initialises = true) : IReadingProvider<ThermalSensor>
    {
        public string Name => name;

        public IEnumerable<ThermalSensor> Read() => [new ThermalSensor(), new ThermalSensor()];

        public bool TryInitialise() => initialises;
    }

    [Fact]
    public void Log_Gives_A_Working_Providers_Reading_Count()
    {
        string       name     = $"Probe {Guid.NewGuid()}";
        FakeProvider provider = new(name);

        using TraceCapture capture = new();

        new ProviderSet<ThermalSensor>("ThermalService", "sensors").Log([new(name, "OK", provider)], new() { [provider] = 3 });

        Assert.Equal([$"ThermalService: {name} OK, 3 sensors"], capture.LinesContaining(name));
    }

    // Providers are probed again every few seconds. Only a different outcome is worth a line.
    [Fact]
    public void Log_Writes_Again_Only_When_A_Result_Changes()
    {
        string                     name      = $"Probe {Guid.NewGuid()}";
        ProviderSet<ThermalSensor> providers = new("ThermalService", "sensors");

        using TraceCapture capture = new();

        providers.Log([new(name, "NOT FOUND, not installed", null)], new());
        providers.Log([new(name, "NOT FOUND, not installed", null)], new());
        providers.Log([new(name, "FAILED, access denied", null)], new());

        Assert.Equal(
            [
                $"ThermalService: {name} NOT FOUND, not installed",
                $"ThermalService: {name} FAILED, access denied"
            ],
            capture.LinesContaining(name));

        Assert.Contains("ThermalService: providers changed", capture.Lines);
    }

    [Fact]
    public void Read_Merges_The_Providers_That_Initialised_And_Logs_Them_Once()
    {
        string                     working   = $"Working {Guid.NewGuid()}";
        string                     missing   = $"Missing {Guid.NewGuid()}";
        ProviderSet<ThermalSensor> providers = new("ThermalService", "sensors");

        using TraceCapture capture = new();

        providers.Build([new FakeProvider(working), new FakeProvider(missing, initialises: false)]);

        Assert.Equal(2, providers.Read().Count);
        Assert.Equal(2, providers.Read().Count);
        Assert.Equal([$"ThermalService: {working} OK, 2 sensors"], capture.LinesContaining(working));
        Assert.Equal([$"ThermalService: {missing} NOT FOUND"], capture.LinesContaining(missing));
    }
}
