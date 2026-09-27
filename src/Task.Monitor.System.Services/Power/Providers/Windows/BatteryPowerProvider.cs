#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Power.Providers.Windows;

internal sealed class BatteryPowerProvider : IPowerProvider
{
    public string Name => "Battery";

    public bool TryInitialise() => true;

    public IEnumerable<PowerReading> Read()
    {
        if (PowerBase.SystemDischargeWatts() is { } watts) {
            yield return new PowerReading {
                Component = PowerComponent.System,
                Rail = "System (battery)",
                Watts = watts,
                Source = PowerSource.Battery
            };
        }
    }

    public void Dispose() { }
}
#endif
