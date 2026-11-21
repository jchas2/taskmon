#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static class UserAccounts
{
    public static uint? GetConsoleUserId()
    {
        nint nameRef;
        uint uid;

        try {
            nameRef = SystemConfiguration.SCDynamicStoreCopyConsoleUser(nint.Zero, out uid, out _);
        }
        catch (EntryPointNotFoundException) {
            return null;
        }

        using CFScope name = new(nameRef);

        if (name.IsNull) {
            return null;
        }

        string? userName = CFConvert.GetString(name);

        return userName is null or "loginwindow"
            ? null
            : uid;
    }

    public static unsafe string? GetUserUuid(uint uid)
    {
        byte* uuid = stackalloc byte[16];

        if (Membership.mbr_uid_to_uuid(uid, uuid) != 0) {
            return null;
        }

        return new Guid(new ReadOnlySpan<byte>(uuid, 16), bigEndian: true)
            .ToString("D")
            .ToUpperInvariant();
    }
}
#endif
