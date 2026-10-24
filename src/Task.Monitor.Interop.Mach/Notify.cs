using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class Notify
{
    public const string ThermalPressureLevel = "com.apple.system.thermalpressurelevel";

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern uint notify_cancel(int token);

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern uint notify_get_state(int token, out ulong state);

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern uint notify_register_check(string name, out int token);
}
