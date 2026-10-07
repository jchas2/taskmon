using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class DyLib
{
    [DllImport(Libraries.LibSystemDyLib)]
    public static extern uint _dyld_image_count();

    [DllImport(Libraries.LibSystemDyLib)]
    public static extern IntPtr _dyld_get_image_name(uint imageIndex);

    internal static unsafe IntPtr ReadExportedPointer(string library, string symbol)
    {
        if (!NativeLibrary.TryLoad(library, out IntPtr handle) ||
            !NativeLibrary.TryGetExport(handle, symbol, out IntPtr address)) {

            return IntPtr.Zero;
        }

        return *(IntPtr*)address;
    }
}
