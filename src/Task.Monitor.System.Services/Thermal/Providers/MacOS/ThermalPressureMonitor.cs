#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Thermal.Providers.MacOS;

internal sealed class ThermalPressureMonitor : IDisposable
{
    private int  token = -1;
    private bool registerFailed;

    public string Diagnostic { get; private set; } = string.Empty;

    public void Dispose()
    {
        if (token >= 0) {
            Notify.notify_cancel(token);
        }

        token          = -1;
        registerFailed = false;
    }

    public ThermalPressure? Read()
    {
        if (token < 0) {
            if (registerFailed) {
                return null;
            }

            uint registerStatus = Notify.notify_register_check(Notify.ThermalPressureLevel, out int registered);

            if (registerStatus != 0) {
                Diagnostic     = $"FAILED, notify_register_check returned notify status {registerStatus}";
                registerFailed = true;
                return null;
            }

            token = registered;
        }

        uint stateStatus = Notify.notify_get_state(token, out ulong level);

        if (stateStatus != 0) {
            Diagnostic = $"FAILED, notify_get_state returned notify status {stateStatus}";
            return null;
        }

        Diagnostic = "OK";
        return ToPressure(level);
    }

    internal static ThermalPressure? ToPressure(ulong level) => level switch {
        0 => ThermalPressure.Nominal,
        1 => ThermalPressure.Moderate,
        2 => ThermalPressure.Heavy,
        3 => ThermalPressure.Trapping,
        4 => ThermalPressure.Sleeping,
        _ => null
    };
}
#endif
