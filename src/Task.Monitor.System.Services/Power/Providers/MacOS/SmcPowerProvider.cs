#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Power.Providers.MacOS;

internal sealed class SmcPowerProvider : IPowerProvider
{
    private const string SystemTotalKey = "PSTR";

    private Smc? smc;

    public string Diagnostic { get; private set; } = string.Empty;

    public void Dispose()
    {
        smc?.Dispose();
        smc = null;
    }

    public string Name => "SMC";

    public IEnumerable<PowerReading> Read()
    {
        if (smc?.ReadValue(SystemTotalKey) is { } watts && watts > 0) {
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
            Diagnostic = "SKIPPED, the smart battery reports system power";
            return false;
        }

        smc = Smc.Open(out string diagnostic);

        if (smc is null) {
            Diagnostic = diagnostic;
            return false;
        }

        if (smc.ReadValue(SystemTotalKey) is null) {
            Diagnostic = $"NOT FOUND, the SMC has no {SystemTotalKey} key";
            return false;
        }

        Diagnostic = "OK";
        return true;
    }
}
#endif
