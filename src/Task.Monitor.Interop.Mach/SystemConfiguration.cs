using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// SystemConfiguration/SCDynamicStoreCopySpecific.h.
public static class SystemConfiguration
{
    [DllImport(Libraries.SystemConfiguration)]
    private static extern IntPtr SCDynamicStoreCopyConsoleUser(IntPtr store, out uint uid, out uint gid);

    // The uid of the user logged in at the console, or null at the login window or when nobody
    // is (e.g. a headless or SSH-only session).
    public static uint? GetConsoleUserId()
    {
        IntPtr nameRef;
        uint uid;

        try {
            nameRef = SCDynamicStoreCopyConsoleUser(IntPtr.Zero, out uid, out _);
        }
        catch (EntryPointNotFoundException) {
            return null;
        }

        using CFScope name = new(nameRef);

        if (name.IsNull) {
            return null;
        }

        string? userName = CoreFoundation.GetString(name);

        // The login window reports itself as the console user.
        return userName is null or "loginwindow" ? null : uid;
    }
}
