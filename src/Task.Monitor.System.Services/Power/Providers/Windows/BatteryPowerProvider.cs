#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Power.Providers.Windows;

internal sealed class BatteryPowerProvider : IPowerProvider
{
    public void Dispose() { }

    public string Name => "Battery";

    public IEnumerable<PowerReading> Read()
    {
        if (PowerBase.SystemDischargeWatts() is { } watts) {
            yield return new PowerReading {
                Component = PowerComponent.System,
                Rail      = "System (battery)",
                Watts     = watts,
                Source    = PowerSource.Battery
            };
        }
    }

    public bool TryInitialise() => true;
}
#endif
