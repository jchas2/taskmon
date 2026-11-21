#if __WIN32__
using Task.Monitor.System.Native;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Power.Providers.Windows;

internal sealed class BatteryPowerProvider : IReadingProvider<PowerReading>
{
    public string Name => "Battery";

    public IEnumerable<PowerReading> Read()
    {
        if (SystemBattery.GetDischargeWatts() is { } watts) {
            yield return new PowerReading {
                Component = PowerComponent.System,
                Rail      = PowerRails.SystemBattery,
                Watts     = watts,
                Source    = PowerSource.Battery
            };
        }
    }
}
#endif
