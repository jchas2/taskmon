using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// Spotlight (Metadata framework) attributes, read from the Spotlight index rather than the disk.
public static class Metadata
{
    public const string kMDItemLogicalSize = "kMDItemLogicalSize";

    [DllImport(Libraries.CoreServices)]
    public static extern nint MDItemCopyAttribute(nint item, nint name);

    [DllImport(Libraries.CoreServices)]
    public static extern nint MDItemCreate(nint allocator, nint path);
}
