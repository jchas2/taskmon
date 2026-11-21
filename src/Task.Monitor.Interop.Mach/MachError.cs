using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class MachError
{
    [DllImport(Libraries.LibSystemDyLib)]
    public static extern nint mach_error_string(int errorValue);
}
