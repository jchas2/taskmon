#if __APPLE__
using System.Runtime.InteropServices;

namespace Task.Monitor.System.Native;

// Some framework constants, such as kSMDomainUserLaunchd, are exported as data symbols rather than
// functions, so they can't be declared with DllImport.
internal static class NativeExports
{
    internal static unsafe nint ReadPointer(string library, string symbol)
    {
        if (!NativeLibrary.TryLoad(library, out nint handle) ||
            !NativeLibrary.TryGetExport(handle, symbol, out nint address)) {

            return nint.Zero;
        }

        return *(nint*)address;
    }
}
#endif
