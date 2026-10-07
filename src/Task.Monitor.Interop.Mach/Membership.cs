using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// membership.h: the uid to directory services GeneratedUID mapping.
public static class Membership
{
    [DllImport(Libraries.LibSystemDyLib)]
    private static extern unsafe int mbr_uid_to_uuid(uint uid, byte* uu);

    // The user's GeneratedUID, e.g. root is FFFFEEEE-DDDD-CCCC-BBBB-AAAA00000000. Null when the
    // uid has no directory services record.
    public static unsafe string? GetUserUuid(uint uid)
    {
        byte* uuid = stackalloc byte[16];

        if (mbr_uid_to_uuid(uid, uuid) != 0) {
            return null;
        }

        // uuid_t is big endian, which is how Guid reads it with bigEndian: true.
        return new Guid(new ReadOnlySpan<byte>(uuid, 16), bigEndian: true).ToString("D").ToUpperInvariant();
    }
}
