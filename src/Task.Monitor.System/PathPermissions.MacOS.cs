using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System;

public static partial class PathPermissions
{
#if __APPLE__
    private static void EnsureUserOwnershipInternal(string path)
    {
        string? sudoUidStr = Environment.GetEnvironmentVariable("SUDO_UID");
        string? sudoGidStr = Environment.GetEnvironmentVariable("SUDO_GID");

        if (string.IsNullOrEmpty(sudoUidStr) || string.IsNullOrEmpty(sudoGidStr)) {
            return;
        }
        
        if (int.TryParse(sudoUidStr, out int uid) && int.TryParse(sudoGidStr, out int gid)) {
            if (Libc.chown(path, uid, gid) == 0) {
                Trace.WriteLine($"Changed ownership for path {path} as sudo detected.");                
            }
            else {
                int error = Marshal.GetLastPInvokeError();
                Trace.WriteLine($"Error changing ownership for path {path} : {Marshal.GetPInvokeErrorMessage(error)}");
            }
        }
    }
#endif
}