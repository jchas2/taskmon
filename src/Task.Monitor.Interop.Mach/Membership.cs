using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// membership.h: the uid to directory services GeneratedUID mapping.
public static class Membership
{
    [DllImport(Libraries.LibSystemDyLib)]
    public static extern unsafe int mbr_uid_to_uuid(uint uid, byte* uu);
}
