#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Power.Providers.MacOS;

internal sealed class SmcPowerProvider : IPowerProvider
{
    private const string SystemTotalKey = "PSTR";

    private Smc? smc;

    public void Dispose()
    {
        smc?.Dispose();
        smc = null;
    }

    public string Name => "SMC";

    public IEnumerable<PowerReading> Read()
    {
        if (smc?.ReadFloat(SystemTotalKey) is { } watts && watts > 0) {
            yield return new PowerReading {
                Component = PowerComponent.System,
                Rail      = PowerRails.System,
                Watts     = watts,
                Source    = PowerSource.Smc
            };
        }
    }

    public bool TryInitialise()
    {
        if (SmartBatteryPowerProvider.IsPresent()) {
            return false;
        }

        smc = Smc.Open();

        return smc?.ReadFloat(SystemTotalKey) is not null;
    }
}
#endif
